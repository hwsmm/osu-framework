#include "Track.h"
#include <math.h>
#include <stdlib.h>
#include <string.h>

extern int frequency;
extern int channels;

#define TrackLock(track) LockMutex(track->mutex)
#define TrackUnlock(track) UnlockMutex(track->mutex)

static double GetMsFromIndex(size_t pos)
{
    double second = (double)pos / (double)frequency / (double)channels;
    return second * 1000.0;
}

static size_t GetIndexFromMs(double ms)
{
    ms /= 1000.0;
    ms *= frequency;
    size_t index = (size_t)ms * channels;
    return index;
}

DLLAPI Track *CreateTrack()
{
    Track *track = (Track*)malloc(sizeof(Track));
    if (track == NULL)
        return NULL;

    memset(track, 0, sizeof(Track));

    if (!CreateMutex(track->mutex))
    {
        free(track);
        return NULL;
    }

    return track;
}

DLLAPI void FreeTrack(Track *track)
{
    TrackLock(track);

    free(track->audio);
    StDestroy(&(track->st_wrap));
    track->audio = NULL;

    TrackUnlock(track);

    DestroyMutex(track->mutex);
    free(track);
}

DLLAPI int TrackInitBuffer(Track *track, size_t size)
{
    int ret = 1;
    
    sample_t *temp;
    
    if (track->audio == NULL)
    {
        temp = (sample_t*)malloc(size);
        if (temp == NULL)
            return -1;
        
        TrackLock(track);
    }
    else
    {
        TrackLock(track);
    
        temp = (sample_t*)realloc(track->audio, size);
        if (temp == NULL)
        {
            ret = -1;
            goto exit;
        }
    }


    track->audio = temp;
    track->size = size;
    track->is_loading = true;

exit:
    TrackUnlock(track);
    return ret;
}

// INTENTIONALLY didn't put locks, is it needed? memory barrier may be needed though
DLLAPI int TrackPutData(Track *track, sample_t *audio, size_t size)
{
    if (track->audio == NULL)
        return -1;

    if (track->put_size + size > track->size)
    {
        if (!TrackInitBuffer(track, track->put_size + size + frequency * sizeof(sample_t) * channels))
            return -2;
    }

    memcpy(track->audio + TrackLen(track), audio, size);

    // may use stdatomic.h atomic_thread_fence
    SDL_MemoryBarrierReleaseFunction();

    track->put_size += size;

    return 1;
}

DLLAPI void TrackDonePutting(Track *track)
{
    // if (track->size != track->put_size)
    //     TrackInitBuffer(track, track->put_size);
    // unneeded overhead
    
    track->is_loaded = true;
    track->is_loading = false;
}

DLLAPI void TrackPlay(Track *track)
{
    TrackLock(track);

    track->done = false;
    track->playing = true;
    StResetSoundTouch(&(track->st_wrap));

    TrackUnlock(track);
}

DLLAPI void TrackPause(Track *track)
{
    track->playing = false;
}

DLLAPI bool TrackIsPlaying(Track *track)
{
    return track->playing;
}

DLLAPI void TrackSetVolume(Track *track, double volume, double balance)
{
    track->left = track->right = (float)volume;
    
    if (balance > 0)
        track->left *= balance;
    else if (balance < 0)
        track->right *= -balance;
}

DLLAPI void TrackSetFreqTempo(Track *track, double freq, double tempo)
{
    TrackLock(track);

    track->reverse_playback = freq < 0;

    if ((track->st_wrap.rfreq != 1 || track->st_wrap.tempo != 1) && (freq == 1 && tempo == 1))
    {
        int latency = StGetLatencyInSample(&(track->st_wrap));

        if (track->reverse_playback)
            track->position += latency;
        else
            track->position -= latency;
    }

    StSetFrequencyTempo(&(track->st_wrap), freq, tempo);

    TrackUnlock(track);
}

static int TrackRawFillAudio(Track *track, sample_t *buffer, int max_size)
{
    int read = 0;

    if (track->audio == NULL || track->put_size <= 0)
        goto exit;

    size_t len = TrackLen(track);

    if (!track->is_loaded)
        SDL_MemoryBarrierAcquireFunction();

    if (track->save_seek > 0)
    {
        if (track->position > track->save_seek)
            track->save_seek = 0;

        if (len > track->save_seek)
        {
            track->position = track->save_seek;
            track->save_seek = 0;
        }
        
        if (track->is_loaded)
            track->save_seek = 0;

        if (track->save_seek > 0)
            goto exit;
    }

    if (track->reverse_playback)
    {
        for (; read < max_size; read += 2)
        {
            *(buffer + read + 1) = *(track->audio + track->position--);
            *(buffer + read) = *(track->audio + track->position);

            if (track->position <= 0)
                break;
            else
                track->position--;
        }
    }
    else
    {
        size_t remain = len - track->position;
        read = remain > (size_t)max_size ? max_size : (int)remain;

        if (buffer != NULL)
            memcpy(buffer, track->audio + track->position, read * sizeof(sample_t));

        track->position += read;
    }

    if ((track->reverse_playback ? track->position <= 0 : track->position >= len) && track->is_loaded)
    {
        track->done = true;
    }

exit:
    return read;
}

static void FillNeededSamplesToSt(Track *track)
{
    TrackLock(track);

    int cur;

    while (!track->done && (cur = StGetProcessedSampleNum(&(track->st_wrap))) < track->prepare_size)
    {
        cur = (int)ceil(((track->prepare_size - cur) / channels) * track->st_wrap.rfreq * track->st_wrap.tempo) * channels;

        size_t backup = track->position;
        cur = TrackRawFillAudio(track, NULL, cur);
        if (cur <= 0)
            break;

        StPrepareAudio(&(track->st_wrap), track->audio + backup, cur); // this doesn't support reverse playback
    }

    if (!track->st_wrap.done_filling && track->done)
        StFlush(&(track->st_wrap));

    TrackUnlock(track);
}

DLLAPI void UpdateTrack(Track *track)
{
    if (track->prepare_size > 0 && CheckRate(track) && track->last_st_pos != track->position)
    {
        track->last_st_pos = track->position;
        FillNeededSamplesToSt(track);
    }
}

DLLAPI bool TrackIsLoading(Track* track)
{
    return track->is_loading;
}

DLLAPI bool TrackIsLoaded(Track* track)
{
    return track->is_loaded;
}

DLLAPI bool TrackIsDone(Track *track)
{
    if (CheckRate(track))
        return track->done && track->st_wrap.done_playing;

    return track->done;
}

DLLAPI double TrackGetPosition(Track *track)
{
    if (!CheckRate(track))
        return GetMsFromIndex(track->position);

    return track->reverse_playback
           ? GetMsFromIndex(track->position) + track->st_wrap.cache_latency_ms
           : GetMsFromIndex(track->position) - track->st_wrap.cache_latency_ms;
}

DLLAPI void TrackSetPosition(Track *track, double position)
{
    position = position < 0 ? 0 : position;
    size_t index = GetIndexFromMs(position);

    TrackLock(track);

    size_t len = TrackLen(track);

    if (!track->is_loaded && index > len)
    {
        track->save_seek = index;
    }
    else
    {
        track->save_seek = 0;
        track->position = index > (len - 1) ? (len - 1) : index;
        track->done = false;
        StResetSoundTouch(&(track->st_wrap));
    }

    TrackUnlock(track);
}

int TrackFillAudio(Track *track, sample_t *buffer, int max_size)
{
    if (track->put_size == 0 && !track->is_loaded)
        return 0;
    
    int ret = 0;

    TrackLock(track);

    if (track->st_wrap.rfreq == 0)
    {
        // do nothing because freq is 0
    }
    else if (CheckRate(track))
    {
        track->prepare_size = max_size;
        FillNeededSamplesToSt(track);
        ret = StFillAudio(&(track->st_wrap), buffer, max_size);

        track->playing = !track->st_wrap.done_playing;
    }
    else
    {
        ret = TrackRawFillAudio(track, buffer, max_size);

        track->playing = !track->done;
    }

    TrackUnlock(track);

    return ret;
}

// CAUSES segfaults occasionally, need to find out a safer way
DLLAPI int TrackPeek(Track *track, float *buffer, int buffer_size, double position)
{
    if (track->audio == NULL)
        return 0;
        
    position = position < 0 ? 0 : position;

    size_t idx = GetIndexFromMs(position);
    size_t len = TrackLen(track); // may cause thread weirdness
    idx = idx > len ? len : idx;
    len = len - idx;
    len = len > (unsigned int)buffer_size ? (unsigned int)buffer_size : len;
    
    if (len <= 0)
        return 0;

#ifdef FLOAT_SAMPLE
    memcpy(buffer, track->audio + idx, len * sizeof(sample_t));
#else
    for (unsigned int i = 0; i < len; i++)
        *(buffer + i) = ToFloat(*(track->audio + idx + i));
#endif

    return (int)len;
}

