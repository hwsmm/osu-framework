#pragma once
#include "LinkedList.h"
#include "Mixer.h"
#include "Config.h"
#include <stdbool.h>
#include <SDL3/SDL.h>

typedef void (*LogFunction)(int, char*);

typedef struct
{
    Node *mixers;
    SDL_AudioStream *audio_stream;

    uint8_t *output_buf;
    int output_buf_size;
} AudioManager;

DLLAPI AudioManager *AllocAudioManager();
DLLAPI void FreeAudioManager(AudioManager *manager);

DLLAPI SDL_AudioDeviceID OpenAudioDevice(AudioManager *manager);
DLLAPI void CloseAudioDevice(AudioManager *manager);

DLLAPI int AddMixer(AudioManager *manager, Mixer *mixer);
DLLAPI int RemoveMixer(AudioManager *manager, Mixer *mixer);

DLLAPI void GetAudioFormat(int *freq, int *channels, SDL_AudioFormat *format);
