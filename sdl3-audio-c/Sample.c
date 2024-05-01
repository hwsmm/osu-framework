#include "Sample.h"
#include <stdlib.h>
#include <stdint.h>
#include <string.h>
#include <float.h>

extern int frequency;
extern int channels;

DLLAPI sample_t *PrepareData(sample_t *data, int size)
{
    sample_t *new = (sample_t*)malloc(size);
    if (new == NULL)
        return NULL;

    memcpy(new, data, size);
    return new;
}

DLLAPI void FreeData(sample_t *data)
{
    free(data);
}

DLLAPI void SamplePlay(Sample *sample)
{
    sample->started = false;
    sample->done = false;
    sample->playing = true;
}

DLLAPI void SamplePause(Sample *sample)
{
    sample->playing = false;
}

DLLAPI bool SampleIsPlaying(Sample *sample)
{
    return sample->playing;
}

DLLAPI void SampleSetVolume(Sample *sample, double volume, double balance)
{
    sample->left = sample->right = (float)volume;
    
    if (balance > 0)
        sample->left *= balance;
    else if (balance < 0)
        sample->right *= -balance;
}

DLLAPI double SampleGetFrequency(Sample *sample)
{
    return sample->rfreq;
}

static int SampleRawFillAudio(Sample *sample, sample_t *buffer, int max_size)
{
    if (sample->done)
        return 0;
    
    if (sample->size <= 0 || sample->audio == NULL)
    {
        sample->playing = false;
        sample->done = true;
        return 0;
    }

    if (!sample->started)
    {
        sample->position = 0;
        sample->started = true;
        sample->done = false;
    }

    int i = 0;

    while (i < max_size)
    {
        int remain = sample->size - sample->position;
        int put = max_size - i;
        if (put > remain)
            put = remain;

        if (buffer != NULL)
            memcpy(buffer + i, sample->audio + sample->position, put * sizeof(sample_t));

        sample->position += put;
        i += put;

        if (sample->position >= sample->size)
        {
            if (sample->loop)
            {
                sample->position = 0;

                if (buffer == NULL)
                    break; // This is called from resampler callback!
            }
            else
            {
                sample->done = true;
                break;
            }
        }
    }

    return i;
}

static long ResampleCallback(void *cb_data, float **data)
{
    Sample *sample = (Sample*)cb_data;
    const int wanted = 4096;

    sample_t *backup = sample->audio + sample->position;
    int got = SampleRawFillAudio(sample, NULL, wanted);

#ifdef FLOAT_SAMPLE
    *data = backup;
#else
    if (sample->resample_buffer == NULL)
        sample->resample_buffer = (float *)malloc(wanted * sizeof(float));

    if (sample->resample_buffer == NULL)
        return 0;

    src_short_to_float_array(backup, sample->resample_buffer, got);

    *data = sample->resample_buffer;
#endif
    return got / channels;
}

DLLAPI void SampleSetFrequency(Sample *sample, double rfreq)
{
    double diff = sample->rfreq > rfreq ? sample->rfreq - rfreq : rfreq - sample->rfreq;
    if (diff > DBL_EPSILON)
    {
        sample->rfreq = rfreq;
        
        if (sample->resample == NULL)
            sample->resample = src_callback_new(ResampleCallback, SRC_LINEAR, channels, &(sample->resample_error), sample);
    }
}

DLLAPI Sample *CreateSample(sample_t *data, int size)
{
    Sample *sample = (Sample*)malloc(sizeof(Sample));
    if (sample == NULL)
        return NULL;

    memset(sample, 0, sizeof(Sample));

    sample->size = size / sizeof(sample_t);
    sample->audio = data;
    sample->rfreq = 1;
    return sample;
}

DLLAPI void FreeSample(Sample *sample)
{
    if (sample->resample != NULL)
        src_delete(sample->resample);

    if (sample->resample_buffer != NULL)
        free(sample->resample_buffer);

#ifndef FLOAT_SAMPLE
    if (sample->resample_store != NULL)
        free(sample->resample_store);
#endif

    free(sample);
}

DLLAPI void ResetSample(Sample* sample)
{
    sample->started = false;
}

DLLAPI bool SampleGetLoop(Sample* sample)
{
    return sample->loop;
}

DLLAPI void SampleSetLoop(Sample* sample, bool loop)
{
    sample->loop = loop;
}

DLLAPI bool SampleIsDone(Sample *sample)
{
    return sample->done;
}

int SampleFillAudio(Sample *sample, sample_t *buffer, int max_size)
{
    double rfreq = sample->rfreq;
    int got = 0;
    
    if (rfreq == 1.0 || sample->resample == NULL)
    {
        got = SampleRawFillAudio(sample, buffer, max_size);
    }
    else if (rfreq > 0 && sample->resample != NULL)
    {
#ifdef FLOAT_SAMPLE
        got = (int)src_callback_read(sample->resample, 1.0 / rfreq, max_size / channels, buffer) * channels;
#else
        if (max_size > sample->resample_store_size)
        {
            if (sample->resample_store != NULL)
                free(sample->resample_store);

            sample->resample_store = (float*)malloc(max_size * sizeof(float));
            if (sample->resample_store == NULL)
                return 0;

            sample->resample_store_size = max_size;
        }

        got = (int)src_callback_read(sample->resample, 1.0 / rfreq, max_size / channels, sample->resample_store) * channels;

        src_float_to_short_array(sample->resample_store, buffer, got);
#endif
    }
    
    if (got < max_size && sample->done)
        sample->playing = false;

    return got;
}
