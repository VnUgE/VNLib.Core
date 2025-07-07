cmake_minimum_required(VERSION 3.18)

project(
	vnlib_compress 
	LANGUAGES C
	DESCRIPTION "An all-in-one compression encoding api in C"
	HOMEPAGE_URL "https://www.vaughnnugent.com/resources/modules/vnlib.core"
)

set(_COMP_PROJ_NAME "vnlib_compress")

#set options for enable botli and zlib
option(COMPRESS_ENABLE_BROTLI "Enable brotli compression" ON)
option(COMPRESS_ENABLE_ZLIB "Enable zlib compression" ON)
option(COMPRESS_ENABLE_ZSTD "Enable zstd compression" ON)
option(COMPRESS_BUILD_SHARED "Produces a shared library instead of a static library" ON)
option(USE_STATIC_RUNTIME "Use the static runtime library" OFF)
set(CMAKE_BUILD_TYPE "Release" CACHE STRING "The build configuration type")

# declare options for native heap linking
set(NATIVE_HEAP_NAME "" CACHE STRING "Name of the native heap if linking to NativeHeapApi.h")
set(NATIVE_HEAP_BIN "" CACHE PATH "Path to the native heap binary directory")
set(NATIVE_HEAP_SRC "" CACHE PATH "Path to the native heap source directory")
set(NATIVE_HEAP_INCLUDES "" CACHE PATH "Path to the native heap includes directory")

string(TOLOWER ${CMAKE_BUILD_TYPE} build_type)
message(STATUS "Build type is '${build_type}'")

set(_native_heap_lib)

#the compression source file is required, all other sources will be added
set(VNLIB_COMPRESS_SOURCES 
	src/platform.h
	src/compression.c
	src/compression.h
)

###############################
#
#	ENABLE VENDORED DEPENDENCIES
#		
###############################

if(COMPRESS_ENABLE_BROTLI)

	message(STATUS "Enabling Brotli compression module from local source")

	set(BROTLI_BUNDLED_MODE ON CACHE BOOL "enable inclusion in 3rd party projects" FORCE)
	set(BROTLI_BUNDLED_MODE OFF CACHE BOOL "explicitly set bundled mode to off" FORCE)
	set(BROTLI_EMSCRIPTEN OFF CACHE BOOL "disable emscripten support" FORCE)
	set(BUILD_SHARED_LIBS OFF CACHE BOOL "disable shared library building, as only static is needed" FORCE)
	set(BROTLI_BUILD_TOOLS OFF CACHE BOOL "disable building of tools" FORCE)
	set(BROTLI_DISABLE_TESTS OFF CACHE BOOL "disable building of tests" FORCE)

	# May cause issues force disabiling the shared library build because this is a CMake global variable
	set(BUILD_SHARED_LIBS OFF CACHE BOOL "disable shared library building, as only static is needed" FORCE)

	add_subdirectory(${CMAKE_CURRENT_SOURCE_DIR}/brotli EXCLUDE_FROM_ALL)
	
	include_directories(${CMAKE_CURRENT_SOURCE_DIR}/brotli/c/include)

	#add the brotli source files to the project
	list(APPEND VNLIB_COMPRESS_SOURCES src/feature_brotli.c)
	add_compile_definitions(VNLIB_COMPRESSOR_BROTLI_ENABLED)
endif()

if(COMPRESS_ENABLE_ZLIB)

	#by default the cloudlfare fork should build a static lib. It will be large 	
	message(STATUS "Enabling zlib compression module from local source code")

	set(SKIP_INSTALL_ALL ON CACHE BOOL "do not install any zlib files" FORCE)

	add_subdirectory(${CMAKE_CURRENT_SOURCE_DIR}/zlib EXCLUDE_FROM_ALL)

	include_directories(${CMAKE_CURRENT_SOURCE_DIR}/zlib)

	#enable the feature code for zlib and add the source files
	list(APPEND VNLIB_COMPRESS_SOURCES src/feature_zlib.c)
	add_compile_definitions(VNLIB_COMPRESSOR_ZLIB_ENABLED)
endif()

if(COMPRESS_ENABLE_ZSTD)

	message(STATUS "Enabling zstd compression module from local source code")

	set(ZSTD_FRAMEWORK OFF CACHE BOOL "Disable zstd framework" FORCE)
	set(ZSTD_LEGACY_SUPPORT OFF CACHE BOOL "Disable legacy support, as it is not needed" FORCE)
	set(ZSTD_BUILD_PROGRAMS OFF CACHE BOOL "Disable building of programs, as we only need the library" FORCE)
	set(ZSTD_BUILD_TESTS OFF CACHE BOOL "Disable building of tests" FORCE)
	set(ZSTD_BUILD_STATIC ON CACHE BOOL "Enable static library building not shared to link against" FORCE)
	set(ZSTD_BUILD_SHARED OFF CACHE BOOL "Disable shared library building" FORCE)
	set(ZSTD_BUILD_COMPRESSION ON CACHE BOOL "Enable compression, as we only need compression" FORCE)
	set(ZSTD_BUILD_DECOMPRESSION OFF CACHE BOOL "Disable decompression, as we only need compression" FORCE)
	set(ZSTD_BUILD_CONTRIB OFF CACHE BOOL "Disable building contrib libraries" FORCE)

	# must point to the zstd build directory
	add_subdirectory(
		${CMAKE_CURRENT_SOURCE_DIR}/zstd/build/cmake
		${CMAKE_CURRENT_BINARY_DIR}/zstd
	)

	include_directories(${CMAKE_CURRENT_SOURCE_DIR}/zstd/lib)
	
	#add the zstd source files to the project
	list(APPEND VNLIB_COMPRESS_SOURCES src/feature_zstd.c)
	add_compile_definitions(VNLIB_COMPRESSOR_ZSTD_ENABLED)
endif()

if(COMPRESS_RPMALLOC_SOURCE_DIR)

	message(STATUS "Enabling static build of rpmalloc")
	
	set(ENABLE_STATIC_FPIC ON CACHE BOOL "Enables rpmallocs static build with FPIC because it's linking with a static library" FORCE)

	#add the rpmalloc source files to the project
	add_subdirectory(
		${COMPRESS_RPMALLOC_SOURCE_DIR} 
		${CMAKE_CURRENT_BINARY_DIR}/vnlib_rpmalloc
		EXCLUDE_FROM_ALL
	)

	# set native heap linking variables
	set(_native_heap_lib vnlib_rpmalloc_static)
	set(NATIVE_HEAP_INCLUDES ${COMPRESS_RPMALLOC_SOURCE_DIR})

endif()


###############################
#
#	CONFIGURE LIBRARY BUILD
#
###############################

set(CMAKE_C_STANDARD_REQUIRED ON)

if(COMPRESS_BUILD_SHARED)
	add_library(${_COMP_PROJ_NAME} SHARED ${VNLIB_COMPRESS_SOURCES})
	#enable position independent code (for shared libraries with exports)
	set_target_properties(${_COMP_PROJ_NAME} PROPERTIES POSITION_INDEPENDENT_CODE ON)
else()
	set(_COMP_PROJ_NAME ${_COMP_PROJ_NAME}_static) #append static to the name
	add_library(${_COMP_PROJ_NAME} STATIC ${VNLIB_COMPRESS_SOURCES})
endif()

target_compile_features(${_COMP_PROJ_NAME} PRIVATE c_std_99)		#force compiler to use c99 standard for library

#if on unix lib will be appended, so we can adjust
if(UNIX)
	set_target_properties(${_COMP_PROJ_NAME} PROPERTIES OUTPUT_NAME vn_compress)
endif()

#since were building in tree, set the export definitions
target_compile_definitions(${_COMP_PROJ_NAME} PRIVATE VNLIB_COMPRESS_EXPORTING)

if(COMPRESS_ENABLE_BROTLI)
	#link the encoder-only library to the main project
	target_link_libraries(${_COMP_PROJ_NAME} PRIVATE brotlienc)	
endif()

if(COMPRESS_ENABLE_ZLIB)
	#workaround cloudflare fork bug. SSE is always enabled with x64 on Windows
    if(MSVC)
		target_compile_definitions(zlib PRIVATE HAS_SSE42 PRIVATE HAS_SSE2)
	endif()

	target_link_libraries(${_COMP_PROJ_NAME} PRIVATE zlib)
endif()

if(COMPRESS_ENABLE_ZSTD)
	#link the zstd library to the main project
	target_link_libraries(${_COMP_PROJ_NAME} PRIVATE libzstd_static)
endif()

#setup flags for windows compilation
if(MSVC)
	target_compile_options(
		${_COMP_PROJ_NAME}
		PRIVATE

		/Qspectre 
		/sdl
		/TC
		/GS 

		#disable warnings for struct padding and spectre mitigation when WX is enabled
		$<$<CONFIG:Debug>:/wd5045>
		$<$<CONFIG:Debug>:/wd4820>

		#for debug configs
		$<$<CONFIG:Debug>:/options:strict>
		$<$<CONFIG:Debug>:/FC>			#full path in diagnostics
		$<$<CONFIG:Debug>:/Wall>
		$<$<CONFIG:Debug>:/WX>			#warnings as errors (only for our project)
		$<$<CONFIG:Debug>:/Zi>			#enable debug info		
		$<$<CONFIG:Debug>:/showIncludes>
	)

	#set build macros
	target_compile_definitions(
		${_COMP_PROJ_NAME}
		PRIVATE

		$<$<CONFIG:DEBUG>:DEBUG>
		$<$<CONFIG:RELEASE>:RELEASE>
	)

#configure gcc flags
elseif(CMAKE_COMPILER_IS_GNUCC)

	target_compile_options(
		${_COMP_PROJ_NAME}
		PRIVATE

		-Wextra
		-fstack-protector
	)

	#enable debug compiler options
	if(build_type STREQUAL "debug")
		target_compile_options(
			${_COMP_PROJ_NAME}
			PRIVATE

			-g				#enable debugger info
			-Og				#disable optimizations
			-Wall			#enable all warnings
			-Werror			#treat warnings as errors
			-pedantic		#enable pedantic mode
		)

		target_compile_definitions(${_COMP_PROJ_NAME} PRIVATE DEBUG)

	endif()

else()
	message(FATAL_ERROR "Unsupported compiler, sorry. Submit an issue for your platform and I'll work on it :)")
endif()

if(NATIVE_HEAP_NAME)

	if(_native_heap_lib)
		message(WARN " Native heap library already linked to the project, overriding library linkage")
	endif()

	find_library(
		_native_heap_lib
		NAMES 
		  ${NATIVE_HEAP_NAME}_static	#attempt to load static library first
		  ${NATIVE_HEAP_NAME}
		
		HINTS 
		  ${NATIVE_HEAP_BIN}
		  ${NATIVE_HEAP_BIN}/${CMAKE_BUILD_TYPE}
		  ${NATIVE_HEAP_BIN}/build
		  ${NATIVE_HEAP_BIN}/build/${CMAKE_BUILD_TYPE}

		  ${NATIVE_HEAP_SRC}  
		  ${NATIVE_HEAP_SRC}/${CMAKE_BUILD_TYPE}
		  ${NATIVE_HEAP_SRC}/build
		  ${NATIVE_HEAP_SRC}/build/${CMAKE_BUILD_TYPE}

		NO_CACHE
		REQUIRED
	)
	
endif()

if(_native_heap_lib)

	message(STATUS "Linking native heap library to the main project found at ${_native_heap_lib}")

	#If manual heap linking is enabled, we need to link the native heap library
	target_link_libraries(${_COMP_PROJ_NAME} PRIVATE ${_native_heap_lib})

	target_compile_definitions(${_COMP_PROJ_NAME} PRIVATE VNLIB_CUSTOM_MALLOC_ENABLE)	#enable native heap memory overrides

endif()

if(NATIVE_HEAP_INCLUDES)
	#add the native heap includes to the project
	include_directories(${NATIVE_HEAP_INCLUDES})
endif()