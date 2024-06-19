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

DLLAPI BiQuadEntry *ApplyBiquadFilter(Mixer *mixer, BiQuadEntry *entry, biquad_coeff *param, int priority)
{
    if (entry == NULL)
    {
        entry = (BiQuadEntry *)malloc(sizeof(BiQuadEntry));
        if (entry != NULL)
            memset(entry, 0, sizeof(BiQuadEntry));
    }
    else // Update
    {
        MixerLock(mixer);
        memcpy(&(entry->filter.coeff), param, sizeof(biquad_coeff));
        MixerUnlock(mixer);

        return entry;
    }

    memcpy(&(entry->filter.coeff), param, sizeof(biquad_coeff));
    entry->priority = priority;

    MixerLock(mixer);

    if (mixer->biquad_list == NULL || ((BiQuadEntry *)(mixer->biquad_list->pointer))->priority >= priority)
    {
        AddNode(&(mixer->biquad_list), entry);
    }
    else
    {
        Node *target = NULL;

        ITER_LINKED(mixer->biquad_list, node,
            {
                if (node->next != NULL)
                {
                    BiQuadEntry *it = (BiQuadEntry *)node->next->pointer;
                    if (it->priority < priority)
                        target = node->next;
                }
            });

        AddNodeAfter(&target, entry);
    }

    MixerUnlock(mixer);

    return entry;
}

DLLAPI int RemoveBiquadFilter(Mixer *mixer, BiQuadEntry *entry)
{
    int i = RemoveList(mixer, &(mixer->biquad_list), entry);
    free(entry);
    return i;
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

    if (mixer->biquad_list != NULL)
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
    
    ITER_LINKED_UNBOX(mixer->track_list, Track *, chan,
        {
            if (chan->playing)
            {
                got = TrackFillAudio(chan, mixer->buffer, size);
                MixAudio(abuf, mixer->buffer, got, chan->left, chan->right);
            }
        });
    
    ITER_LINKED_UNBOX(mixer->sample_list, Sample *, chan,
        {
            if (chan->playing)
            {
                got = SampleFillAudio(chan, mixer->buffer, size);
                MixAudio(abuf, mixer->buffer, got, chan->left, chan->right);
            }
        });

    if (mixer->biquad_list != NULL)
    {
        ITER_LINKED_UNBOX(mixer->biquad_list, BiQuadEntry *, bq,
            {
                for (int e = 0; e < size; e += 2)
                {
                    BiQuad(abuf + e, abuf + e + 1, &(bq->filter));
                }
            });

        MixAudio((sample_t *)put, abuf, size, 1.0f, 1.0f);
    }

    MixerUnlock(mixer);
}
