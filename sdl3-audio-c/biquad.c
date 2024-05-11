/* Simple implementation of Biquad filters -- Tom St Denis
 *
 * Based on the work

Cookbook formulae for audio EQ biquad filter coefficients
---------------------------------------------------------
by Robert Bristow-Johnson, pbjrbj@viconet.com  a.k.a. robert@audioheads.com

 * Available on the web at

http://www.smartelectronix.com/musicdsp/text/filters005.txt

 * Enjoy.
 *
 * This work is hereby placed in the public domain for all purposes, whether
 * commercial, free [as in speech] or educational, etc.  Use the code and please
 * give me credit if you wish.
 *
 * Tom St Denis -- http://tomstdenis.home.dhs.org
*/

#include "biquad.h"
#include <stdlib.h>

#define biquad_internal(lr, res, smp) \
    res = b->a0 * smp + b->a1 * b->x1##lr + b->a2 * b->x2##lr - \
          b->a3 * b->y1##lr - b->a4 * b->y2##lr; \
    b->x2##lr = b->x1##lr; \
    b->x1##lr = smp; \
    b->y2##lr = b->y1##lr; \
    b->y1##lr = res;
    
/* Below this would be biquad.c */
/* Computes a BiQuad filter on a sample */
void BiQuad(sample_t *left, sample_t *right, biquad * b)
{
    smp_type sample;
    
#ifndef FLOAT_SAMPLE
    smp_type result;
    
    sample = ToFloat(*left);
    biquad_internal(l, result, sample);
    *left = FromFloat(result);
    
    sample = ToFloat(*right);
    biquad_internal(r, result, sample);
    *right = FromFloat(result);
#else
    sample = *left;
    biquad_internal(l, *left, sample);
    
    sample = *right;
    biquad_internal(r, *right, sample);
#endif
}

/* sets up a BiQuad Filter */
DLLAPI biquad *BiQuadNew(enum biquad_type type, double dbGain, double freq,
                         double srate, double q)
{
    biquad *b;
    double A, omega, sn, cs, alpha, beta;
    double a0, a1, a2, b0, b1, b2;

    /* setup variables */
    A = pow(10.0, dbGain /40.0);
    omega = 2.0 * M_PI * freq /srate;
    sn = sin(omega);
    cs = cos(omega);
    alpha = sn / (2.0 * q);
    beta = sqrt(A + A);

    switch (type)
    {
    case LPF:
        b0 = (1 - cs) /2;
        b1 = 1 - cs;
        b2 = (1 - cs) /2;
        a0 = 1 + alpha;
        a1 = -2 * cs;
        a2 = 1 - alpha;
        break;
    case HPF:
        b0 = (1 + cs) /2;
        b1 = -(1 + cs);
        b2 = (1 + cs) /2;
        a0 = 1 + alpha;
        a1 = -2 * cs;
        a2 = 1 - alpha;
        break;
    case BPF:
        b0 = sn /2;
        b1 = 0;
        b2 = -sn /2;
        a0 = 1 + alpha;
        a1 = -2 * cs;
        a2 = 1 - alpha;
        break;
    case BPQ:
        b0 = alpha;
        b1 = 0;
        b2 = -alpha;
        a0 = 1 + alpha;
        a1 = -2 * cs;
        a2 = 1 - alpha;
        break;
    case NOTCH:
        b0 = 1;
        b1 = -2 * cs;
        b2 = 1;
        a0 = 1 + alpha;
        a1 = -2 * cs;
        a2 = 1 - alpha;
        break;
    case PEQ:
        b0 = 1 + (alpha * A);
        b1 = -2 * cs;
        b2 = 1 - (alpha * A);
        a0 = 1 + (alpha /A);
        a1 = -2 * cs;
        a2 = 1 - (alpha /A);
        break;
    case LSH:
        b0 = A * ((A + 1) - (A - 1) * cs + beta * sn);
        b1 = 2 * A * ((A - 1) - (A + 1) * cs);
        b2 = A * ((A + 1) - (A - 1) * cs - beta * sn);
        a0 = (A + 1) + (A - 1) * cs + beta * sn;
        a1 = -2 * ((A - 1) + (A + 1) * cs);
        a2 = (A + 1) + (A - 1) * cs - beta * sn;
        break;
    case HSH:
        b0 = A * ((A + 1) + (A - 1) * cs + beta * sn);
        b1 = -2 * A * ((A - 1) + (A + 1) * cs);
        b2 = A * ((A + 1) + (A - 1) * cs - beta * sn);
        a0 = (A + 1) - (A - 1) * cs + beta * sn;
        a1 = 2 * ((A - 1) - (A + 1) * cs);
        a2 = (A + 1) - (A - 1) * cs - beta * sn;
        break;
    case APF:
        b0 = 1 - alpha;
        b1 = -2 * cs;
        b2 = 1 + alpha;
        a0 = 1 + alpha;
        a1 = -2 * cs;
        a2 = 1 - alpha;
        break;
    default:
        return NULL;
    }

    b = malloc(sizeof(biquad));
    if (b == NULL)
        return NULL;

    /* precompute the coefficients */
    b->a0 = (float)(b0 /a0);
    b->a1 = (float)(b1 /a0);
    b->a2 = (float)(b2 /a0);
    b->a3 = (float)(a1 /a0);
    b->a4 = (float)(a2 /a0);

    /* zero initial samples */
    b->x1l = b->x2l = b->x1r = b->x2r = 0;
    b->y1l = b->y2l = b->y1r = b->y2r = 0;

    return b;
}
/* crc==3062280887, version==4, Sat Jul  7 00:03:23 2001 */

DLLAPI void BiQuadFree(biquad *filter)
{
    free(filter);
}
