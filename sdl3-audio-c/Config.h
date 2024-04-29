#pragma once

// #define FLOAT_SAMPLE

#ifndef FLOAT_SAMPLE
typedef short sample_t;

#define SAMPLE_MAX_VAL 32767
#define SAMPLE_MIN_VAL -32768

#define ToFloat(x) (float)(x / 32767.0f)
#define FromFloat(x) (short)(x * 32767.0f)
#else
typedef float sample_t;

#define SAMPLE_MAX_VAL 1.0f
#define SAMPLE_MIN_VAL -1.0f

#define ToFloat(x) (float)(x)
#define FromFloat(x) (float)(x)
#endif

// #define USE_C_MUTEX

#ifndef USE_C_MUTEX
#include <SDL3/SDL.h>

typedef SDL_Mutex *mutex_t;

#define CreateMutex(mutex) ((mutex = SDL_CreateMutex()) != NULL)
#define DestroyMutex(mutex) SDL_DestroyMutex(mutex)

#define LockMutex(mutex) SDL_LockMutex(mutex)
#define UnlockMutex(mutex) SDL_UnlockMutex(mutex)
#else
#include <threads.h>

typedef mtx_t mutex_t;

#define CreateMutex(mutex) (mtx_init(&(mutex), mtx_plain | mtx_recursive) == thrd_success)
#define DestroyMutex(mutex) mtx_destroy(&(mutex))

#define LockMutex(mutex) mtx_lock(&(mutex))
#define UnlockMutex(mutex) mtx_unlock(&(mutex))
#endif

#define DEFAULT_FREQUENCY 44100
#define DEFAULT_CHANNELS 2
