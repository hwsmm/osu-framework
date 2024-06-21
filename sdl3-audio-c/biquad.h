// brought from https://www.musicdsp.org/en/latest/Filters/64-biquad-c-code.html
// added more types, and use q instead of bandwidth
#pragma once

/* this would be biquad.h */
#include "SymbolPrefix.h"
#include "Config.h"
#include <math.h>

#ifndef M_LN2
#define M_LN2	   0.69314718055994530942
#endif

#ifndef M_PI
#define M_PI		3.14159265358979323846
#endif

/* whatever sample type you want */
typedef float smp_type;

typedef struct
{
    float a0, a1, a2, a3, a4;
}
biquad_coeff;

/* this holds the data required to update samples thru a filter */
typedef struct
{
    biquad_coeff coeff;
    smp_type x1, x2, y1, y2;
}
biquad;

/* filter types */
enum biquad_type
{
    LPF, /* low pass filter */
    HPF, /* High pass filter */
    BPF, /* band pass filter */
    BPQ, /* band pass filter (constant skirt gain) */
    NOTCH, /* Notch Filter */
    PEQ, /* Peaking band EQ filter */
    LSH, /* Low shelf filter */
    HSH, /* High shelf filter */
    APF /* all pass filter */
};

inline smp_type BiQuad(smp_type sample, biquad * b)
{
    smp_type result;
    
    result = b->coeff.a0 * sample + b->coeff.a1 * b->x1 + b->coeff.a2 * b->x2 -
             b->coeff.a3 * b->y1 - b->coeff.a4 * b->y2;
    
    b->x2 = b->x1;
    b->x1 = sample;
    
    b->y2 = b->y1;
    b->y1 = result;
    
    return result;
}

DLLAPI void BiQuadUpdate(biquad_coeff *b, enum biquad_type type, double dbGain, /* gain of filter */
                         double freq,                     /* center frequency */
                         double srate,                    /* sampling rate */
                         double q);                       /* bandwidth */
