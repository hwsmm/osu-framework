#pragma once
#include "SymbolPrefix.h"
#include "SoundTouchWrapper.h"
#include "Config.h"
#include <stdbool.h>
#include <stdatomic.h>
#include <stdint.h>

typedef struct
{
    volatile bool playing;

    volatile float left;
    volatile float right;

    mutex_t mutex;

    sample_t *audio;
    size_t size; // in bytes
    _Atomic size_t put_size;
    volatile bool is_loading;
    volatile bool is_loaded;

    _Atomic size_t position; // in sample_t size
    volatile size_t save_seek;
    bool reverse_playback;

    volatile bool done;

    SoundTouchWrapper st_wrap;
    size_t last_st_pos;
    volatile int prepare_size;
} Track;

#define TrackLen(track) (track->put_size / sizeof(sample_t))
#define CheckRate(track) (track->st_wrap.tempo != 1 || track->st_wrap.rfreq != 1)

DLLAPI Track *CreateTrack();
DLLAPI void FreeTrack(Track *track);

DLLAPI int TrackInitBuffer(Track *track, size_t size);
DLLAPI int TrackPutData(Track *track, sample_t *audio, size_t size);
DLLAPI void TrackDonePutting(Track *track);

DLLAPI void TrackSetFreqTempo(Track *track, double freq, double tempo);

DLLAPI bool TrackIsLoading(Track *track);
DLLAPI bool TrackIsLoaded(Track *track);
DLLAPI bool TrackIsDone(Track *track);

DLLAPI void TrackPlay(Track *track);
DLLAPI void TrackPause(Track *track);
DLLAPI bool TrackIsPlaying(Track *track);

DLLAPI void TrackSetVolume(Track *track, double volume, double balance);
DLLAPI double TrackGetPosition(Track *track);
DLLAPI void TrackSetPosition(Track *track, double position);

DLLAPI void UpdateTrack(Track *track);

int TrackFillAudio(Track *track, sample_t *buffer, int max_size);
DLLAPI int TrackPeek(Track *track, float *buffer, int buffer_size, double position);
