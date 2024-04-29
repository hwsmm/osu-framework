#!/bin/sh
NAME=libofsdlaudiow.so
OUTPUT=bin
rm -rf $OUTPUT
mkdir -p $OUTPUT
echo output to $OUTPUT/$NAME

SMP=${SMP:-FLOAT_SAMPLE}
ARGS="-Ofast -march=native"
ARGS+=" -D$SMP"
echo Using $ARGS

gcc $ARGS -fPIC -rdynamic -shared \
-o $OUTPUT/$NAME *.c \
-lSDL3 -lSoundTouchDll -lsamplerate \
-Wno-unused-parameter || echo FAILED
