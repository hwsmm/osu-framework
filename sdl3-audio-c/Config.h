#pragma once

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

#ifndef USE_C_MUTEX
#include <SDL3/SDL.h>

typedef SDL_Mutex *mutex_t;

#define CreateMutex(mutex) ((mutex = SDL_CreateMutex()) != NULL)
#define DestroyMutex(mutex) SDL_DestroyMutex(mutex)

#define LockMutex(mutex) SDL_LockMutex(mutex)
#define UnlockMutex(mutex) SDL_UnlockMutex(mutex)

#define MemoryBarrierRelease() SDL_MemoryBarrierReleaseFunction()
#define MemoryBarrierAcquire() SDL_MemoryBarrierAcquireFunction()

#else
#include <threads.h>
#include <stdatomic.h>

typedef mtx_t mutex_t;

#define CreateMutex(mutex) (mtx_init(&(mutex), mtx_plain | mtx_recursive) == thrd_success)
#define DestroyMutex(mutex) mtx_destroy(&(mutex))

#define LockMutex(mutex) mtx_lock(&(mutex))
#define UnlockMutex(mutex) mtx_unlock(&(mutex))

#define MemoryBarrierRelease() atomic_thread_fence(memory_order_release)
#define MemoryBarrierAcquire() atomic_thread_fence(memory_order_acquire)

#endif

#define DEFAULT_FREQUENCY 44100
#define DEFAULT_CHANNELS 2
