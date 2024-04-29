#include "SoundTouchWrapper.h"
#include <stdint.h>
#include <stddef.h>

extern int frequency;
extern int channels;

void StInit(SoundTouchWrapper *st)
{
    if (st->soundtouch != NULL)
        return;

    st->soundtouch = soundtouch_createInstance();
    soundtouch_setSampleRate(st->soundtouch, frequency);
    soundtouch_setChannels(st->soundtouch, channels);

    soundtouch_setSetting(st->soundtouch, /*quickseek*/ 2, 1);
    soundtouch_setSetting(st->soundtouch, /*overlapms*/ 5, 4);
    soundtouch_setSetting(st->soundtouch, /*seqms*/ 3, 30);
}

void StDestroy(SoundTouchWrapper *st)
{
    if (st->soundtouch == NULL)
        return;

    soundtouch_destroyInstance(st->soundtouch);
    st->soundtouch = NULL;
}

int StGetProcessedSampleNum(SoundTouchWrapper *st)
{
    return soundtouch_numSamples(st->soundtouch) * channels;
}

int StGetLatencyInSample(SoundTouchWrapper *st)
{
    if (st->soundtouch == NULL)
        return 0;

    float processed = (float)soundtouch_numSamples(st->soundtouch) * (float)st->rfreq * (float)st->tempo;
    return (soundtouch_numUnprocessedSamples(st->soundtouch) + (int)processed) * channels;
}

double StGetLatencyInMs(SoundTouchWrapper *st)
{
    return ((double)StGetLatencyInSample(st) / (double)channels * 1000.0 / (double)frequency);
}

void StSetFrequencyTempo(SoundTouchWrapper *st, double rfreq, double tempo)
{
    if (st->rfreq == rfreq && st->tempo == tempo)
        return;

    st->rfreq = rfreq;
    st->tempo = tempo;

    if ((rfreq == 0 || tempo == 0) || (rfreq == 1 && tempo == 1))
    {
        StResetSoundTouch(st); // don't dispose for now since it can be reused
        return;
    }
    
    if (rfreq < 0.05)
        rfreq = 0.05;
        
    if (tempo < 0.05)
        tempo = 0.05;

    if (st->soundtouch == NULL)
        StInit(st);

    float ratechange = (float)(rfreq - 1.0f) * 100.0f;
    soundtouch_setRateChange(st->soundtouch, ratechange < -95 ? -95 : ratechange > 5000 ? 5000 : ratechange);

    float tempochange = (float)(tempo - 1.0f) * 100.0f;
    soundtouch_setTempoChange(st->soundtouch, tempochange < -95 ? -95 : tempochange > 5000 ? 5000 : tempochange);
}

void StResetSoundTouch(SoundTouchWrapper *st)
{
    st->done_filling = false;
    st->done_playing = false;
    st->cache_latency_ms = 0;

    if (st->soundtouch != NULL)
        soundtouch_clear(st->soundtouch);
}

void StFlush(SoundTouchWrapper *st)
{
    st->done_filling = true;

    if (st->soundtouch != NULL)
        soundtouch_flush(st->soundtouch);
}

void StPrepareAudio(SoundTouchWrapper *st, sample_t *buffer, int size)
{
    if (st->soundtouch == NULL)
        return;

#ifdef FLOAT_SAMPLE
    soundtouch_putSamples(st->soundtouch, buffer, size / channels);
#else
    soundtouch_putSamples_i16(st->soundtouch, buffer, size / channels);
#endif
}

int StFillAudio(SoundTouchWrapper *st, sample_t *buffer, int max_size)
{
    if (st->soundtouch == NULL)
        return 0;

    unsigned int got;

#ifdef FLOAT_SAMPLE
    got = soundtouch_receiveSamples(st->soundtouch, buffer, max_size / channels) * channels;
#else
    got = soundtouch_receiveSamples_i16(st->soundtouch, buffer, max_size / channels) * channels;
#endif

    st->cache_latency_ms = StGetLatencyInMs(st);

    if (st->done_filling && (int)got < max_size)
        st->done_playing = true;

    return (int)got;
}
