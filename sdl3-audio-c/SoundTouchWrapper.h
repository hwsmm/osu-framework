#pragma once
#include "Config.h"
#include <stdbool.h>
#include <stdatomic.h>
#include <SoundTouchDLL.h>

typedef struct
{
    HANDLE soundtouch;
    double tempo;
    double rfreq;
    volatile bool done_playing;
    volatile bool done_filling;

    _Atomic double cache_latency_ms;
} SoundTouchWrapper;

void StInit(SoundTouchWrapper *st);
void StDestroy(SoundTouchWrapper *st);

void StSetFrequencyTempo(SoundTouchWrapper *st, double rfreq, double tempo);
void StResetSoundTouch(SoundTouchWrapper *st);

int StGetProcessedSampleNum(SoundTouchWrapper *st);
int StGetLatencyInSample(SoundTouchWrapper *st);
double StGetLatencyInMs(SoundTouchWrapper *st);

void StFlush(SoundTouchWrapper *st);
void StPrepareAudio(SoundTouchWrapper *st, sample_t *buffer, int size);
int StFillAudio(SoundTouchWrapper *st, sample_t *buffer, int max_size);
