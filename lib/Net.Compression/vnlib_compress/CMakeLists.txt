cmake_minimum_required(VERSION 3.10)

project(vnlib_compress C)
set(CMAKE_PROJECT_NAME "vnlib_compress")

#set options for enable botli and zlib
option(ENABLE_BROTLI "Enable brotli compression" ON)
option(ENABLE_ZLIB "Enable zlib compression" ON)
option(ENABLE_RPMALLOC "Enable local source code vnlib_rpmalloc memory allocator" OFF)
option(COMPRESS_BUILD_SHARED "Produces a shared library instead of a static library" ON)
option(USE_STATIC_RUNTIME "Use the static runtime library" OFF)
set(CMAKE_BUILD_TYPE "Release" CACHE STRING "The build configuration type")

string(TOLOWER ${CMAKE_BUILD_TYPE} build_type)
message(STATUS "Build type is '${build_type}'")

#export all header files to the main project
file(GLOB COMP_HEADERS *.h)
set(VNLIB_COMPRESS_SOURCES 
	compression.c
)

include(FetchContent)

###############################
#
#	DOWNLOAD DEPENDENCIES
#		
###############################

if(ENABLE_BROTLI)

	message(STATUS "Downloading brotli compression as a local dependency")

	set(BROTLI_BUNDLED_MODE OFF)	#explicitly set bundled mode to off
	set(BROTLI_EMSCRIPTEN OFF)		#disable emscripten support
	set(BUILD_SHARED_LIBS OFF)		#disable shared library building, as only static is needed

	FetchContent_Declare(
	  lib_brotli
	  GIT_REPOSITORY		https://github.com/google/brotli.git
	  GIT_TAG				1b3a5ccb6e7b9384b741437532f4dae0730c61f2
	  GIT_PROGRESS			TRUE
	)

	FetchContent_GetProperties(lib_brotli)
	FetchContent_MakeAvailable(lib_brotli)

	#add include directories for brotli
	include_directories(${lib_brotli_SOURCE_DIR}/c/include)

	#add the brotli source files to the project
	list(APPEND VNLIB_COMPRESS_SOURCES feature_brotli.c)
	add_compile_definitions(VNLIB_COMPRESSOR_BROTLI_ENABLED)
endif()

if(ENABLE_ZLIB)

	#by default the cloudlfare fork should build a static lib. It will be large 	
	message(STATUS "Downloading ZLIB as a local dependency")

	set(SKIP_INSTALL_ALL ON)	#do not install zlib files

	#add zlib source files to the project
	FetchContent_Declare(
	  lib_cf_zlib
	  GIT_REPOSITORY		https://github.com/cloudflare/zlib.git
	  GIT_TAG				92530568d2c128b4432467b76a3b54d93d6350bd
	  GIT_PROGRESS			TRUE
	)

	#Get project properties
	FetchContent_GetProperties(lib_cf_zlib)
	FetchContent_MakeAvailable(lib_cf_zlib)

	#add include directories for zlib
	include_directories(${lib_cf_zlib_SOURCE_DIR})

	#enable the feature code for zlib and add the source files
	list(APPEND VNLIB_COMPRESS_SOURCES feature_zlib.c)
	add_compile_definitions(VNLIB_COMPRESSOR_ZLIB_ENABLED)
endif()

#Add support for rpmalloc memmory allocator
if(ENABLE_RPMALLOC)

	#enable greedy mode for rpmalloc
	set(ENABLE_GREEDY ON)

	add_subdirectory(
		../../Utils.Memory/vnlib_rpmalloc/
		${CMAKE_CURRENT_BINARY_DIR}/vnlib_rpmalloc/
	)

	add_compile_definitions(VNLIB_CUSTOM_MALLOC_ENABLE)
endif()


#Setup the compiler options 
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)	#enable position independent code (for shared libraries with exports)

###############################
#
#	CONFIGURE LIBRARY BUILD
#
###############################

if(COMPRESS_BUILD_SHARED)
	set(TARGET_NAME ${CMAKE_PROJECT_NAME})
	add_library(${TARGET_NAME} SHARED ${VNLIB_COMPRESS_SOURCES} ${COMP_HEADERS})
else()
	set(TARGET_NAME ${CMAKE_PROJECT_NAME}_static)
	add_library(${TARGET_NAME} STATIC ${VNLIB_COMPRESS_SOURCES} ${COMP_HEADERS})
endif()

target_compile_features(${TARGET_NAME} PRIVATE c_std_90)		#force compiler to use c90 standard for library

#if on unix lib will be appended, so we can adjust
if(UNIX)
	set_target_properties(${TARGET_NAME} PROPERTIES OUTPUT_NAME vn_compress)
endif()

#since were buildiing in tree, set the export defintiions
target_compile_definitions(${TARGET_NAME} PRIVATE VNLIB_EXPORTING)

if(ENABLE_BROTLI)
	#link the encoder-only library to the main project
	target_link_libraries(${TARGET_NAME} PRIVATE brotlienc)	
endif()

if(ENABLE_ZLIB)
	#workaround cloudlfare fork bug. SSE is always enabled with x64 on Windows
    if(MSVC)
		target_compile_definitions(zlib PRIVATE HAS_SSE42 PRIVATE HAS_SSE2)
	endif()

	target_link_libraries(${TARGET_NAME} PRIVATE zlib)
endif()

#link rpmalloc to the main project
if(ENABLE_RPMALLOC)		
	target_link_libraries(${TARGET_NAME} PRIVATE vnlib_rpmalloc_static)
	add_dependencies(${TARGET_NAME} vnlib_rpmalloc_static)

	#Include the nativeheap api header
	target_include_directories(${TARGET_NAME} PRIVATE ../../Utils.Memory/vnlib_rpmalloc/)
endif()

#setup flags for windows compilation
if(MSVC)
	target_compile_options(
		${TARGET_NAME}
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
		${TARGET_NAME}
		PRIVATE

		$<$<CONFIG:DEBUG>:DEBUG>
		$<$<CONFIG:RELEASE>:RELEASE>
	)

#configure gcc flags
elseif(CMAKE_COMPILER_IS_GNUCC)

	target_compile_options(
		${TARGET_NAME}
		PRIVATE

		-Wextra
		-fstack-protector
	)

	#enable debug compiler options
	if(build_type STREQUAL "debug")
		target_compile_options(
			${TARGET_NAME}
			PRIVATE

			-g				#enable debugger info
			-Og				#disable optimizations
			-Wall			#enable all warnings
			-Werror			#treat warnings as errors
			-pedantic		#enable pedantic mode
		)

		target_compile_definitions(${TARGET_NAME} PRIVATE DEBUG)

	endif()

else()
	message(FATAL_ERROR "Unsupported compiler, sorry. Submit an issue for your platform and I'll work on it :)")
endif()

if(NATIVE_HEAP_LIB_PATH)

	#Include the nativeheap api header
	include_directories(${NATIVE_HEAP_INCLUDES})
	
	#If manual heap linking is enabled, we need to link the native heap library
	target_link_libraries(${TARGET_NAME} PRIVATE ${NATIVE_HEAP_LIB_PATH})
	target_compile_definitions(${TARGET_NAME} PRIVATE VNLIB_CUSTOM_MALLOC_ENABLE)	#configure src

endif()