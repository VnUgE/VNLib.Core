cmake_minimum_required(VERSION 3.0)

project(vnlib_compress C)

#export header files to the main project
file(GLOB COMP_HEADERS *.h)

#Add indepednent source files to the project
set(VNLIB_COMPRESS_SOURCES compression.c)

#set options for enable botli and zlib
option(ENABLE_BROTLI "Enable brotli compression" ON)
option(ENABLE_ZLIB "Enable zlib compression" ON)
option(ENABLE_RPMALLOC "Enable local source code vnlib_rpmalloc memory allocator" OFF)

set(THIRD_PARTY_DIR ./third-party)

#add feature specific source files to the project
if(ENABLE_BROTLI)
	list(APPEND VNLIB_COMPRESS_SOURCES feature_brotli.c)
	#define the brotli feature macro to enable zlib support
	add_definitions(-DVNLIB_COMPRESSOR_BROTLI_ENABLED)
endif()

if(ENABLE_ZLIB)
	list(APPEND VNLIB_COMPRESS_SOURCES feature_zlib.c)
	#define the zlib feature macro to enable zlib support
	add_definitions(-DVNLIB_COMPRESSOR_ZLIB_ENABLED)
endif()

#create my shared library
add_library(${CMAKE_PROJECT_NAME} SHARED ${VNLIB_COMPRESS_SOURCES} ${COMP_HEADERS})
#also create static library
add_library(${CMAKE_PROJECT_NAME}_static STATIC ${VNLIB_COMPRESS_SOURCES} ${COMP_HEADERS})

#if on unix lib will be appended, so we can adjust
if(UNIX)
	set_target_properties(${CMAKE_PROJECT_NAME} ${CMAKE_PROJECT_NAME}_static PROPERTIES OUTPUT_NAME vn_compress)
endif()


#Setup the compiler options 
set(CMAKE_C_STANDARD 90)
set(CMAKE_C_STANDARD_REQUIRED ON)

#enable position independent code (for shared libraries with exports)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

#since were buildiing in tree, set the export defintiions
add_definitions(-DVNLIB_EXPORTING)

message(STATUS "Build type is '${CMAKE_BUILD_TYPE}'")

#include checks for zlib and brotli
include(CheckTypeSize)
include(CheckFunctionExists)
include(CheckIncludeFile)
include(CheckCCompilerFlag)
enable_testing()

#setup flags for windows compilation
if(MSVC)
	
    #global windows cl flags
	add_compile_options(
		/Qspectre 
		/sdl
		/TC
		/GS 
		
		$<$<CONFIG:Debug>:/FC>
		$<$<CONFIG:Debug>:/showIncludes>		
	)

	#only target our project
	target_compile_options(
		${CMAKE_PROJECT_NAME}
		PRIVATE

		#for debug configs
		$<$<CONFIG:Debug>:/options:strict>
		#disable warnings for struct padding and spectre mitigation wuen WX is enabled
		$<$<CONFIG:Debug>:/wd5045>
		$<$<CONFIG:Debug>:/wd4820>
		$<$<CONFIG:Debug>:/Wall>
		$<$<CONFIG:Debug>:/WX>	#warnings as errors (only for our project)
		$<$<CONFIG:Debug>:/Zi>
		$<$<CONFIG:Debug>:/Zo>
	)

	#set build macros
	add_compile_definitions( 
		$<$<CONFIG:DEBUG>:DEBUG>
		$<$<CONFIG:RELEASE>:RELEASE>
	)

#configure gcc flags
elseif(CMAKE_COMPILER_IS_GNUCC)

	add_compile_options(
		-Wextra
		-fstack-protector

		$<$<CONFIG:Debug>:-g>
		$<$<CONFIG:Debug>:-Og>
		$<$<CONFIG:Debug>:-Wall>
		$<$<CONFIG:Debug>:-Werror>
	)

	#only target our project
    target_compile_options(
		${CMAKE_PROJECT_NAME}
		PRIVATE
		$<$<CONFIG:Debug>:-Wall>
		$<$<CONFIG:Debug>:-pedantic>
	)

endif()

#check for brotli encoding only feature enablement
if(ENABLE_BROTLI)	

	#macros for brotli
	set(BROTLI_DEFINITIONS)
	
	set(LIBM_LIBRARY)
	check_function_exists(log2 LOG2_RES)

	if(NOT LOG2_RES)

	  set(orig_req_libs "${CMAKE_REQUIRED_LIBRARIES}")
	  set(CMAKE_REQUIRED_LIBRARIES "${CMAKE_REQUIRED_LIBRARIES};m")

	  check_function_exists(log2 LOG2_LIBM_RES)

	  if(LOG2_LIBM_RES)
		set(LIBM_LIBRARY "m")
		list(APPEND BROTLI_DEFINITIONS -DBROTLI_HAVE_LOG2=1)
	  else()
		list(APPEND BROTLI_DEFINITIONS -DBROTLI_HAVE_LOG2=0)
	  endif()

	  set(CMAKE_REQUIRED_LIBRARIES "${orig_req_libs}")
	  unset(LOG2_LIBM_RES)
	  unset(orig_req_libs)
	else()
	  list(APPEND BROTLI_DEFINITIONS -DBROTLI_HAVE_LOG2=1)
	endif()
	unset(LOG2_RES)

	#target definitions from brotli cmakelists
	if(${CMAKE_SYSTEM_NAME} MATCHES "Linux")
		list(APPEND BROTLI_DEFINITIONS -DOS_LINUX)
	elseif(${CMAKE_SYSTEM_NAME} MATCHES "FreeBSD")
		list(APPEND BROTLI_DEFINITIONS -DOS_FREEBSD)
	elseif(${CMAKE_SYSTEM_NAME} MATCHES "Darwin")
		list(APPEND BROTLI_DEFINITIONS -DOS_MACOSX)
		set(CMAKE_MACOS_RPATH TRUE)
	endif()

	#add the include directory for brotli so we can include the header files
	include_directories(${THIRD_PARTY_DIR}/brotli/c/include)

	#get common sources
	file(GLOB BROTLI_SOURCES ${THIRD_PARTY_DIR}/brotli/c/common/*.c)

	#we need to add the brotli encoder source files to the project
	file(GLOB BROTLI_ENC_SOURCES ${THIRD_PARTY_DIR}/brotli/c/enc/*.c)

	#add brotli as a static library to link later
	add_library(lib_brotli STATIC ${BROTLI_SOURCES} ${BROTLI_ENC_SOURCES})
	
	target_link_libraries(${CMAKE_PROJECT_NAME} lib_brotli)

	#add the definitions to the brotli project only
	target_compile_definitions(lib_brotli PRIVATE ${BROTLI_DEFINITIONS})

endif()

#check for zlib feature enablement, supports madler and cloudflare forks
if(ENABLE_ZLIB)	

	#add the include directory for zlib so we can include the header files
	include_directories(${THIRD_PARTY_DIR}/zlib)

	set(ZLIB_DEFINITIONS)
	set(Z_C_FLAGS)

	#we only need to add the zlib deflate source files to the project
	set(ZLIB_SOURCES 
		${THIRD_PARTY_DIR}/zlib/deflate.c
		${THIRD_PARTY_DIR}/zlib/adler32.c
		${THIRD_PARTY_DIR}/zlib/crc32.c
		${THIRD_PARTY_DIR}/zlib/zutil.c
		${THIRD_PARTY_DIR}/zlib/trees.c
	)
	
	check_type_size(off64_t OFF64_T)
	check_function_exists(fseeko HAVE_FSEEKO)

	if(HAVE_OFF64_T)
		list(APPEND ZLIB_DEFINITIONS -D_LARGEFILE64_SOURCE=1)
	endif()

	#add fseeko if we have it
	if(NOT HAVE_FSEEKO)
		list(APPEND ZLIB_DEFINITIONS -DNO_FSEEKO)
	endif()

	if(MSVC)

		set(CMAKE_DEBUG_POSTFIX "d")
	
		list(APPEND ZLIB_DEFINITIONS
			-D_CRT_SECURE_NO_DEPRECATE 
			-D_CRT_NONSTDC_NO_DEPRECATE
		)

		
		#NOTE
		#During CI a pre-compiled library will be built. 
		#We cannot depend on users having the the same instructions as the build machine
		#So some optimizations are disabled for the pre-compiled library

		if(NOT CI_PRECOMPILE)

			#setup avx compiler support on Windows
			check_c_compiler_flag(/arch:AVX HAS_AVX)
			if (HAS_AVX)
				list(APPEND Z_C_FLAGS /arch:AVX)
				list(APPEND ZLIB_DEFINITIONS -DHAS_AVX)
			endif()

		endif()

		#All x64 machines have SSE2, so we can use it as 
		#and the Windows compiler will automatically use it
		#so we only need to set the definition
		list(APPEND Z_C_FLAGS /arch:SSE2)
		list(APPEND ZLIB_DEFINITIONS -DHAS_SSE2 -DHAS_SSE42)

	elseif(UNIX)	
		
		#for cloudflare intrinsic detections
		check_c_compiler_flag(-march=armv8-a+crc ARM_CRC)
		check_c_compiler_flag(-msse2 HAS_SSE2)
		check_c_compiler_flag(-mssse3 HAS_SSSE3)
		check_c_compiler_flag(-msse4.2 HAS_SSE42)
		check_c_compiler_flag(-mpclmul HAS_PCLMUL)

		if(ARM_CRC)
			list(APPEND Z_C_FLAGS -march=armv8-a+crc)

			if(EXISTS "{THIRD_PARTY_DIR}/zlib/adler32_simd.c")
				list(APPEND ZLIB_DEFINITIONS -DADLER32_SIMD_NEON)
				list(APPEND ZLIB_SOURCES ${THIRD_PARTY_DIR}/zlib/adler32_simd.c)
			endif()

		else()
			if(HAS_SSE2)
				list(APPEND Z_C_FLAGS -msse2)
				list(APPEND ZLIB_DEFINITIONS -DHAS_SSE2)
				#excluding inflate specific optimizations
			endif()
			if(HAS_SSSE3)
				list(APPEND Z_C_FLAGS -mssse3)

				#add cloudflare intrinsic optimizations, may not be present if using non-cloudflare fork
				if(EXISTS "${THIRD_PARTY_DIR}/zlib/adler32_simd.c")
					list(APPEND ZLIB_DEFINITIONS -DHAS_SSSE3 -DADLER32_SIMD_SSSE3)
					list(APPEND ZLIB_SOURCES ${THIRD_PARTY_DIR}/zlib/adler32_simd.c)
				endif()

			endif()
			if(HAS_SSE42)
				list(APPEND Z_C_FLAGS -msse4.2)
				list(APPEND ZLIB_DEFINITIONS -DHAS_SSE42)
			endif()
			if(HAS_PCLMUL)
				list(APPEND Z_C_FLAGS -mpclmul)

				#add cloudflare intrinsic optimizations for PCMLONGMUL crc32, may not be present if using non-cloudflare fork
				if(EXISTS "${THIRD_PARTY_DIR}/zlib/crc32_simd.c")
					list(APPEND ZLIB_DEFINITIONS -DHAS_PCLMUL)
					list(APPEND ZLIB_SOURCES ${THIRD_PARTY_DIR}/zlib/crc32_simd.c)
				endif()				
				
			endif()
		endif()
	endif()

	
	
	#add zlib as a library to link later
	add_library(lib_deflate STATIC ${ZLIB_SOURCES})

	if(MSVC)
		#allways targeting x64 machines
		set_target_properties(lib_deflate PROPERTIES STATIC_LIBRARY_FLAGS "/machine:x64")
	endif()

	#add the definitions to the zlib project only
	target_compile_definitions(lib_deflate PRIVATE ${ZLIB_DEFINITIONS})

	#only target zlib project with compiler flags
	target_compile_options(lib_deflate PRIVATE ${Z_C_FLAGS})
	
	target_link_libraries(${CMAKE_PROJECT_NAME} lib_deflate)
endif()

if(NATIVE_HEAP_LIB_PATH)

	#Include the nativeheap api header
	include_directories(${NATIVE_HEAP_INCLUDES})
	
	#If manual heap linking is enabled, we need to link the native heap library
	target_link_libraries(${CMAKE_PROJECT_NAME} ${NATIVE_HEAP_LIB_PATH})
	
	#Disable rpmalloc if we are linking a custom native heap
	set(ENABLE_RPMALLOC OFF)

	#add defintion to enable custom malloc heap overrides
	target_compile_definitions(${CMAKE_PROJECT_NAME} PRIVATE VNLIB_CUSTOM_MALLOC_ENABLE)

endif()

#Add support for rpmalloc memmory allocator
if(ENABLE_RPMALLOC)

	#Include the nativeheap api header
	include_directories(../../Utils.Memory/NativeHeapApi/src)

	if(MSVC)

		#find the rpmalloc static library for windows builds
		find_library(VNLIB_RPMALLOC_DEBUG_LIB
			NAMES vnlib_rpmalloc_static
			PATHS ../../Utils.Memory/vnlib_rpmalloc/build/Debug
		)

		find_library(VNLIB_RPMALLOC_RELEASE_LIB
			NAMES vnlib_rpmalloc_static
			PATHS ../../Utils.Memory/vnlib_rpmalloc/build/Release
		)

		#target static libraries for all configs
		target_link_libraries(${CMAKE_PROJECT_NAME} 
			$<$<CONFIG:Debug>:${VNLIB_RPMALLOC_DEBUG_LIB}>
			#set release for all release configs
			$<$<CONFIG:MinSizeRel>:${VNLIB_RPMALLOC_RELEASE_LIB}>
			$<$<CONFIG:RelWithDepInfo>:${VNLIB_RPMALLOC_RELEASE_LIB}>
			$<$<CONFIG:Release>:${VNLIB_RPMALLOC_RELEASE_LIB}>
		)

	elseif(UNIX)

		#find the rpmalloc library for unix builds
		find_library(VNLIB_RPMALLOC_LIB
			NAMES libvn_rpmalloc
			PATHS ../../Utils.Memory/vnlib_rpmalloc/build
		)

		#add the rpmalloc library to the project
		target_link_libraries(${CMAKE_PROJECT_NAME} ${VNLIB_RPMALLOC_LIB})

	else()

		#failure 
		message(FATAL_ERROR "Unsupported platform configuration for rpmalloc")

	endif()	

	#add defintion to enable custom malloc heap overrides
	target_compile_definitions(${CMAKE_PROJECT_NAME} PRIVATE VNLIB_CUSTOM_MALLOC_ENABLE)

endif()
