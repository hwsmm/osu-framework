#pragma once
#include "LinkedList.h"
#include "Track.h"
#include "Sample.h"
#include "Config.h"
#include "biquad.h"
#include <stdint.h>

typedef struct
{
    int priority;
    biquad filter;
} BiQuadEntry;

typedef struct
{
    Node *track_list;
    Node *sample_list;

    biquad **filter_list;
    int filter_list_size;

    Node *biquad_list;

    mutex_t mutex;

    sample_t *buffer;
    int buffer_size;

    sample_t *filter_buffer;
    int filter_buffer_size;
} Mixer;

DLLAPI Mixer *CreateMixer();
DLLAPI void FreeMixer(Mixer *mixer);

DLLAPI int AddTrack(Mixer *mixer, Track *channel);
DLLAPI int RemoveTrack(Mixer *mixer, Track *channel);

DLLAPI int AddSample(Mixer *mixer, Sample *channel);
DLLAPI int RemoveSample(Mixer *mixer, Sample *channel);

DLLAPI BiQuadEntry *ApplyBiquadFilter(Mixer *mixer, BiQuadEntry *entry, biquad_coeff *param, int priority);
DLLAPI int RemoveBiquadFilter(Mixer *mixer, BiQuadEntry *entry);

void MixerFillAudio(Mixer *mixer, uint8_t *buffer, int size);
