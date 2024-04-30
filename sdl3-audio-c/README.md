**Dependencies**
- SDL3
- libsamplerate
- libSoundTouchDLL

It seems that libSoundTouchDLL is not available in Arch by default, probably because Arch official PKGBUILD doesn't pass any flags to build it?

You can try using `soundtouch-git` AUR package, but I don't use Arch in the first place, so I don't really know if it works or not.

---

**Steps**
1. Install dependencies
2. Run `./compile.sh` in this directory, and run `export LD_LIBRARY_PATH=$PWD/bin`
3. Append `AudioDriver = SDL3` in `framework.ini` in osu! data folder

You may get some SDL error logs. This can happen if osu!framework SDL3 doesn't match your system SDL3 which this library is compiled with. Set `SDL3_DYNAMIC_API=/my/actual/libSDL3.so.0` before running osu! to fix this, or just delete `libSDL3.so` in your osu! directory.

You can use `SDL_AUDIO_DEVICE_SAMPLE_FRAMES` to adjust latency. `SDL_AUDIO_DEVICE_SAMPLE_FRAMES=128` means `128/44100`.
