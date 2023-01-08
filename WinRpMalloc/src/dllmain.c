// dllmain.cpp : Defines the entry point for the DLL application.

#include "pch.h"

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
	/*
    * Taken from the malloc.c file for initializing the library.
    * and thread events
    */
    switch (ul_reason_for_call)
    {
        case DLL_PROCESS_ATTACH:
            rpmalloc_initialize();
            break;
        case DLL_THREAD_ATTACH:
            rpmalloc_thread_initialize();
            break;
        case DLL_THREAD_DETACH:
            rpmalloc_thread_finalize(1);
            break;
        case DLL_PROCESS_DETACH:
            rpmalloc_finalize();
            break;
    }
    return TRUE;
}