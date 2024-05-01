#include "Mixer.h"
#include <string.h>
#include <stdlib.h>
#include <math.h>
#include <stdbool.h>

#define MixerLock(mixer) LockMutex(mixer->mutex)
#define MixerUnlock(mixer) UnlockMutex(mixer->mutex)

DLLAPI Mixer *CreateMixer()
{
    Mixer *mixer = (Mixer*)malloc(sizeof(Mixer));
    if (mixer == NULL)
        return NULL;

    memset(mixer, 0, sizeof(Mixer));

    if (!CreateMutex(mixer->mutex))
    {
        free(mixer);
        return NULL;
    }

    return mixer;
}

DLLAPI void FreeMixer(Mixer *mixer)
{
    MixerLock(mixer);

    FreeList(mixer->track_list); // don't free since they may move to parent mixer in C# code
    mixer->track_list = NULL;

    FreeList(mixer->sample_list);
    mixer->sample_list = NULL;

    if (mixer->filter_list != NULL)
    {
        for (int i = 0; i < mixer->filter_list_size; i++)
        {
            if (*(mixer->filter_list + i) != NULL)
                free(*(mixer->filter_list + i));
        }
        free(mixer->filter_list);
    }

    free(mixer->buffer);
    free(mixer->filter_buffer);

    MixerUnlock(mixer);

    DestroyMutex(mixer->mutex);
    free(mixer);
}

static int AddList(Mixer *mixer, Node **list, void *channel)
{
    if (mixer == NULL || list == NULL || channel == NULL)
        return 0;

    MixerLock(mixer);

    AddNode(list, channel);

    MixerUnlock(mixer);

    return *list == NULL ? 0 : 1;
}

static int RemoveList(Mixer *mixer, Node **list, void *channel)
{
    int i = 0;

    if (mixer == NULL || list == NULL || *list == NULL || channel == NULL)
        return 0;

    MixerLock(mixer);

    ITER_LINKED(*list, node,
    {
        if (node->pointer == channel)
        {
            i++;
            RemoveNode(list, node);
        }
    });

    MixerUnlock(mixer);

    return i;
}

DLLAPI int AddTrack(Mixer *mixer, Track *channel)
{
    return AddList(mixer, &(mixer->track_list), channel);
}

DLLAPI int RemoveTrack(Mixer *mixer, Track *channel)
{
    return RemoveList(mixer, &(mixer->track_list), channel);
}

DLLAPI int AddSample(Mixer *mixer, Sample *channel)
{
    return AddList(mixer, &(mixer->sample_list), channel);
}

DLLAPI int RemoveSample(Mixer *mixer, Sample *channel)
{
    return RemoveList(mixer, &(mixer->sample_list), channel);
}

DLLAPI void ReplaceFilterList(Mixer *mixer, biquad **filter_list, int list_size)
{
    biquad **new_list = NULL, **old_list = mixer->filter_list;
    if (list_size > 0)
    {
        new_list = (biquad **)malloc(list_size * sizeof(biquad *));
        if (new_list != NULL)
            memcpy(new_list, filter_list, list_size * sizeof(biquad *));
    }
    
    MixerLock(mixer);

    mixer->filter_list = new_list;
    mixer->filter_list_size = list_size;

    MixerUnlock(mixer);
    
    free(old_list);
}

DLLAPI void RemoveFilter(Mixer *mixer, int index)
{
    void *removed = NULL;
    
    MixerLock(mixer);
    
    if (index >= 0 && mixer->filter_list_size > index)
    {
        removed = *(mixer->filter_list + index);
        *(mixer->filter_list + index) = NULL;
    }
    
    if (index == -2)
    {
        removed = mixer->filter_list;
        mixer->filter_list = NULL;
    }
    
    MixerUnlock(mixer);
    
    if (index == -2)
    {
        biquad **old_list = (biquad**)removed;
        for (int i = 0; i < mixer->filter_list_size; i++)
        {
            free(*(old_list + i));
        }
        
        mixer->filter_list_size = 0;
    }
    else
    {
        free(removed);
    }
}


#ifdef FLOAT_SAMPLE
#define INTERNAL_MIX(ptr, val) ptr += val
#else
#define INTERNAL_MIX(ptr, val) \
if ((val < 0) != (ptr < 0)) \
    ptr += val; \
else \
{ \
    int c = val + ptr; \
    if (c > 32767) \
        c = 32767; \
    else if (c < -32768) \
        c = -32768; \
    ptr = (sample_t)c; \
}
#endif

static inline void MixAudio(sample_t *dest, sample_t *src, int size, float left, float right)
{
    if (size <= 0 || (left <= 0 && right <= 0))
        return;

    for (int i = 0; i < size; i += 2)
    {
        sample_t l = (sample_t)(left * *(src + i));
        sample_t r = (sample_t)(right * *(src + i + 1));
        
        INTERNAL_MIX(*(dest + i), l);
        INTERNAL_MIX(*(dest + i + 1), r);
    }
}

void MixerFillAudio(Mixer *mixer, uint8_t *put, int size)
{
    if (mixer->buffer_size != size)
    {
        if (mixer->buffer != NULL)
            free(mixer->buffer);

        mixer->buffer = (sample_t*)malloc(size);
        if (mixer->buffer == NULL)
            return;

        mixer->buffer_size = size;
    }

    sample_t *abuf = (sample_t*)put;

    int got;

    MixerLock(mixer);

    if (mixer->filter_list_size > 0)
    {
        if (mixer->filter_buffer_size < size)
        {
            if (mixer->filter_buffer != NULL)
                free(mixer->filter_buffer);

            mixer->filter_buffer = (sample_t*)malloc(size);
            if (mixer->filter_buffer == NULL)
                return;

            mixer->filter_buffer_size = size;
        }

        memset(mixer->filter_buffer, 0, size);
        abuf = mixer->filter_buffer;
    }

    size /= sizeof(sample_t);
    
    if (mixer->track_list != NULL)
    {
        ITER_LINKED_UNBOX(mixer->track_list, Track*, chan,
        {
            if (chan->playing)
            {
                got = TrackFillAudio(chan, mixer->buffer, size);
                MixAudio(abuf, mixer->buffer, got, chan->left, chan->right);
            }
        });
    }
    
    if (mixer->sample_list != NULL)
    {
        ITER_LINKED_UNBOX(mixer->sample_list, Sample*, chan,
        {
            if (chan->playing)
            {
                got = SampleFillAudio(chan, mixer->buffer, size);
                MixAudio(abuf, mixer->buffer, got, chan->left, chan->right);
            }
        });
    }

    if (mixer->filter_list_size > 0)
    {
        for (int i = 0; i < mixer->filter_list_size; i++)
        {
            if (*(mixer->filter_list + i) == NULL)
                continue;

            for (int e = 0; e < size; e += 2)
            {
                BiQuad(abuf + e, abuf + e + 1, *(mixer->filter_list + i));
            }
        }

        MixAudio((sample_t*)put, abuf, size, 1.0f, 1.0f);
    }

    MixerUnlock(mixer);
}
