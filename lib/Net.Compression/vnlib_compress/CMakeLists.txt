cmake_minimum_required(VERSION 3.10)

project(vnlib_compress C)
set(_COMP_PROJ_NAME "vnlib_compress")

#set options for enable botli and zlib
option(ENABLE_BROTLI "Enable brotli compression" ON)
option(ENABLE_ZLIB "Enable zlib compression" ON)
option(COMPRESS_BUILD_SHARED "Produces a shared library instead of a static library" ON)
option(USE_STATIC_RUNTIME "Use the static runtime library" OFF)
set(CMAKE_BUILD_TYPE "Release" CACHE STRING "The build configuration type")

string(TOLOWER ${CMAKE_BUILD_TYPE} build_type)
message(STATUS "Build type is '${build_type}'")

include(FetchContent)

#the compression source file is required, all other sources will be added
set(VNLIB_COMPRESS_SOURCES 
	src/util.h
	src/platform.h
	src/compression.c
	src/compression.h
)

###############################
#
#	ENABLE VENDORED DEPENDENCIES
#		
###############################

if(ENABLE_BROTLI)

	message(STATUS "Enabling Brotli compression module from local source")

	set(BROTLI_BUNDLED_MODE ON)		#enable inclusion in 3rd party projects
	set(BROTLI_BUNDLED_MODE OFF)	#explicitly set bundled mode to off
	set(BROTLI_EMSCRIPTEN OFF)		#disable emscripten support
	set(BUILD_SHARED_LIBS OFF)		#disable shared library building, as only static is needed
	set(BROTLI_BUILD_TOOLS OFF)		#disable building of tools
	set(BROTLI_DISABLE_TESTS OFF)	#disable building of tests

	add_subdirectory(${CMAKE_CURRENT_SOURCE_DIR}/brotli EXCLUDE_FROM_ALL)
	
	include_directories(${CMAKE_CURRENT_SOURCE_DIR}/brotli/c/include)

	#add the brotli source files to the project
	list(APPEND VNLIB_COMPRESS_SOURCES src/feature_brotli.c)
	add_compile_definitions(VNLIB_COMPRESSOR_BROTLI_ENABLED)
endif()

if(ENABLE_ZLIB)

	#by default the cloudlfare fork should build a static lib. It will be large 	
	message(STATUS "Enabling zlib compression module from local source code")

	set(SKIP_INSTALL_ALL ON)	#do not install any zlib files

	add_subdirectory(${CMAKE_CURRENT_SOURCE_DIR}/zlib EXCLUDE_FROM_ALL)

	include_directories(${CMAKE_CURRENT_SOURCE_DIR}/zlib)

	#enable the feature code for zlib and add the source files
	list(APPEND VNLIB_COMPRESS_SOURCES src/feature_zlib.c)
	add_compile_definitions(VNLIB_COMPRESSOR_ZLIB_ENABLED)
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

target_compile_features(${_COMP_PROJ_NAME} PRIVATE c_std_99)		#force compiler to use c90 standard for library

#if on unix lib will be appended, so we can adjust
if(UNIX)
	set_target_properties(${_COMP_PROJ_NAME} PROPERTIES OUTPUT_NAME vn_compress)
endif()

#since were buildiing in tree, set the export defintiions
target_compile_definitions(${_COMP_PROJ_NAME} PRIVATE VNLIB_COMPRESS_EXPORTING)

if(ENABLE_BROTLI)
	#link the encoder-only library to the main project
	target_link_libraries(${_COMP_PROJ_NAME} PRIVATE brotlienc)	
endif()

if(ENABLE_ZLIB)
	#workaround cloudlfare fork bug. SSE is always enabled with x64 on Windows
    if(MSVC)
		target_compile_definitions(zlib PRIVATE HAS_SSE42 PRIVATE HAS_SSE2)
	endif()

	target_link_libraries(${_COMP_PROJ_NAME} PRIVATE zlib)
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
	
	message(STATUS "Linking native heap library to the main project found at ${_native_heap_lib}")

	#Include the nativeheap api header
	include_directories(${NATIVE_HEAP_INCLUDES})

	#If manual heap linking is enabled, we need to link the native heap library
	target_link_libraries(${_COMP_PROJ_NAME} PRIVATE ${_native_heap_lib})

	target_compile_definitions(${_COMP_PROJ_NAME} PRIVATE VNLIB_CUSTOM_MALLOC_ENABLE)	#enable native heap memory overrides

endif()