#pragma once

#if defined(_WIN32) || defined(WIN32)

#define DLLAPI __declspec(dllexport)

#else
// GNU version

#if defined(DLL_EXPORTS)
#define DLLAPI __attribute__((__visibility__("default")))
#else
#define DLLAPI
#endif

#endif
