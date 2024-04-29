#pragma once
#include "LinkedList.h"
#include "Track.h"
#include "Sample.h"
#include "Config.h"
#include "biquad.h"
#include <stdint.h>

typedef struct
{
    Node *track_list;
    Node *sample_list;

    biquad **filter_list;
    int filter_list_size;

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

DLLAPI void ReplaceFilterList(Mixer *mixer, biquad **filter_list, int list_size);
DLLAPI void RemoveFilter(Mixer *mixer, int index);

void MixerFillAudio(Mixer *mixer, uint8_t *buffer, int size);
