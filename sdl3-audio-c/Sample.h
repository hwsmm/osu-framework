#pragma once
#include "SymbolPrefix.h"
#include "Config.h"
#include <stdbool.h>
#include <stdatomic.h>
#include <samplerate.h>

typedef struct
{
    volatile bool playing;

    volatile float left;
    volatile float right;

    sample_t *audio;
    int size;

    volatile bool loop;
    volatile int position;

    volatile bool started;
    volatile bool done;

    volatile double rfreq;
    SRC_STATE *resample;
    int resample_error;

    float *resample_buffer;
#ifndef FLOAT_SAMPLE
    float *resample_store;
    int resample_store_size;
#endif
} Sample;

// may just create a new struct for the data
DLLAPI sample_t *PrepareData(sample_t *data, int size);
DLLAPI void FreeData(sample_t *data);

DLLAPI Sample *CreateSample(sample_t *data, int size);
DLLAPI void FreeSample(Sample *sample);
DLLAPI void ResetSample(Sample *sample);

DLLAPI void SamplePlay(Sample *sample);
DLLAPI void SamplePause(Sample *sample);
DLLAPI bool SampleIsPlaying(Sample *sample);

DLLAPI void SampleSetVolume(Sample *sample, double volume, double balance);

DLLAPI double SampleGetFrequency(Sample *sample);
DLLAPI void SampleSetFrequency(Sample *sample, double frequency);

DLLAPI bool SampleGetLoop(Sample *sample);
DLLAPI void SampleSetLoop(Sample *sample, bool loop);
DLLAPI bool SampleIsDone(Sample *sample);

int SampleReturnAudio(Sample *sample, sample_t *temp_buf, int max_size, sample_t **audio);
