#include "AudioManager.h"
#include <stdlib.h>
#include <stdio.h>
#include <string.h>

int frequency;
int channels;
SDL_AudioFormat format;

DLLAPI AudioManager *AllocAudioManager()
{
    AudioManager *manager = (AudioManager*)calloc(1, sizeof(AudioManager));
    if (manager == NULL)
        return NULL;

    return manager;
}

DLLAPI void FreeAudioManager(AudioManager *manager)
{
    if (manager->mixers != NULL)
    {
        // mixer will dispose themselves
        FreeList(manager->mixers);
    }

    CloseAudioDevice(manager);
    free(manager->output_buf);
    free(manager);
}

static void AudioCallback(void *userdata, SDL_AudioStream *stream, int additional_amount, int total_amount)
{
    AudioManager *manager = (AudioManager*)userdata;
    if (manager->mixers == NULL)
        return;

    if (manager->output_buf == NULL || manager->output_buf_size < additional_amount)
    {
        free(manager->output_buf);
        manager->output_buf = (uint8_t*)malloc(additional_amount);

        if (manager->output_buf == NULL)
            return;
    }

    memset(manager->output_buf, 0, additional_amount);

    ITER_LINKED_UNBOX(manager->mixers, Mixer*, mixer,
    {
        MixerFillAudio(mixer, manager->output_buf, additional_amount);
    });

    SDL_PutAudioStreamData(stream, manager->output_buf, additional_amount);
}

DLLAPI SDL_AudioDeviceID OpenAudioDevice(AudioManager *manager)
{
    if (manager->audio_stream != NULL)
    {
        SDL_DestroyAudioStream(manager->audio_stream);
        manager->audio_stream = NULL;
    }

    SDL_AudioSpec audio_spec;
    memset(&audio_spec, 0, sizeof(SDL_AudioSpec));
    GetAudioFormat(&(audio_spec.freq), &(audio_spec.channels), &(audio_spec.format));

    manager->audio_stream = SDL_OpenAudioDeviceStream(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, &audio_spec, AudioCallback, manager);

    if (manager->audio_stream == NULL)
        return 0;

    frequency = audio_spec.freq;
    channels = audio_spec.channels;
    format = audio_spec.format;

    SDL_AudioDeviceID id = SDL_GetAudioStreamDevice(manager->audio_stream);
    SDL_ResumeAudioDevice(id);

    return id;
}

DLLAPI void CloseAudioDevice(AudioManager *manager)
{
    if (manager->audio_stream != NULL)
    {
        SDL_DestroyAudioStream(manager->audio_stream);
        manager->audio_stream = NULL;
    }
}

DLLAPI int AddMixer(AudioManager *manager, Mixer *mixer)
{
    if (manager->audio_stream != NULL)
        SDL_LockAudioStream(manager->audio_stream);

    AddNode(&(manager->mixers), mixer);

    if (manager->audio_stream != NULL)
        SDL_UnlockAudioStream(manager->audio_stream);

    return manager->mixers == NULL ? 0 : 1;
}

DLLAPI int RemoveMixer(AudioManager *manager, Mixer *mixer)
{
    int i = 0;

    if (manager->audio_stream != NULL)
        SDL_LockAudioStream(manager->audio_stream);

    ITER_LINKED(manager->mixers, node,
    {
        if (node->pointer == mixer)
        {
            i++;
            RemoveNode(&(manager->mixers), node);
        }
    });

    if (manager->audio_stream != NULL)
        SDL_UnlockAudioStream(manager->audio_stream);

    return i;
}

DLLAPI void GetAudioFormat(int *freq, int *chns, SDL_AudioFormat *fmt)
{
    *freq = DEFAULT_FREQUENCY;
    *chns = DEFAULT_CHANNELS;
#ifdef FLOAT_SAMPLE
    *fmt = SDL_AUDIO_F32;
#else
    *fmt = SDL_AUDIO_S16;
#endif
}
