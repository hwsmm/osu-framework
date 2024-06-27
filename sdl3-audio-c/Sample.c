#include "Sample.h"
#include <stdlib.h>
#include <stdint.h>
#include <string.h>
#include <math.h>
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
    float fbal = 1.0f - fabsf((float)balance);
    
    if (balance > 0)
        sample->left *= fbal;
    else if (balance < 0)
        sample->right *= fbal;
}

DLLAPI double SampleGetFrequency(Sample *sample)
{
    return sample->rfreq;
}

static int SampleRawReturnAudio(Sample *sample, int max_size, sample_t **audio)
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

    int remain = sample->size - sample->position;
    int put = max_size;
    if (put > remain)
        put = remain;

    *audio = sample->audio + sample->position;
    
    sample->position += put;

    if (sample->position >= sample->size)
    {
        if (sample->loop)
            sample->position = 0;
        else
            sample->done = true;
    }

    return put;
}

static long ResampleCallback(void *cb_data, float **data)
{
    Sample *sample = (Sample*)cb_data;
    const int wanted = 4096;

    sample_t *audio = NULL;
    int got = SampleRawReturnAudio(sample, wanted, &audio);

#ifdef FLOAT_SAMPLE
    *data = audio;
#else
    if (sample->resample_buffer == NULL)
        sample->resample_buffer = (float *)malloc(wanted * sizeof(float));

    if (sample->resample_buffer == NULL)
        return 0;

    src_short_to_float_array(audio, sample->resample_buffer, got);

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
    sample->left = sample->right = 1.0f;
    sample->rfreq = 1.0;
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

int SampleReturnAudio(Sample *sample, sample_t *temp_buf, int max_size, sample_t **audio)
{
    double rfreq = sample->rfreq;
    int got = 0;
    
    if (rfreq == 1.0 || sample->resample == NULL)
    {
        got = SampleRawReturnAudio(sample, max_size, audio);
    }
    else if (rfreq > 0 && sample->resample != NULL)
    {
#ifdef FLOAT_SAMPLE
        got = (int)src_callback_read(sample->resample, 1.0 / rfreq, max_size / channels, temp_buf) * channels;
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

        src_float_to_short_array(sample->resample_store, temp_buf, got);
#endif
        *audio = temp_buf;
    }
    
    if (got < max_size && sample->done)
        sample->playing = false;

    return got;
}
