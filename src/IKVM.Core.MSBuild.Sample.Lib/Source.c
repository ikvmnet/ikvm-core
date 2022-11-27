#include <stdio.h>

#ifdef _WIN32
#define EXPORT __declspec(dllexport)
#else
#if (defined(__GNUC__) && ((__GNUC__ > 4) || (__GNUC__ == 4) && (__GNUC_MINOR__ > 2))) || __has_attribute(visibility)
#define EXPORT __attribute__((visibility("default")))
#else
#define EXPORT
#endif
#endif

EXPORT void Main()
{
    printf('hi');
}
