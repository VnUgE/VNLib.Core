cmake_minimum_required(VERSION 3.0)

project(vnlib_mimalloc C)

#export header files to the main project
file(GLOB HEADERS *.h)

#Add indepednent source files to the project
file(GLOB VNLIB_MIMALLOC_SOURCES *.c)

#add heapapi includes
include_directories(../NativeHeapApi/src)

#set options for greedy allocator defaults
option(ENABLE_GREEDY "Enable greedy allocator configuration" ON)

#create my shared library
add_library(${CMAKE_PROJECT_NAME} SHARED ${VNLIB_MIMALLOC_SOURCES} ${HEADERS})
#also create static library
add_library(${CMAKE_PROJECT_NAME}_static STATIC ${VNLIB_MIMALLOC_SOURCES} ${HEADERS})

#if on unix lib will be appended, so we can adjust
if(UNIX)
	set_target_properties(${CMAKE_PROJECT_NAME} ${CMAKE_PROJECT_NAME}_static PROPERTIES OUTPUT_NAME vn_mimalloc)
endif()

#Setup the compiler options 
set(CMAKE_C_STANDARD 90)
set(CMAKE_C_STANDARD_REQUIRED ON)

#enable position independent code (for shared libraries with exports)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

message(STATUS "Build type is '${CMAKE_BUILD_TYPE}'")

#if debug
add_compile_definitions($<$<CONFIG:Debug>:DEBUG>)

if(ENABLE_GREEDY)
	add_compile_definitions(VNLIB_MIMALLOC_GREEDY)
endif()

#setup flags for windows compilation
if(MSVC)
	
    #global windows cl flags
	add_compile_options(
		/Qspectre 
		/sdl
		/TC
		/GS 
		/machine:x64
		
		$<$<CONFIG:Debug>:/FC>
		$<$<CONFIG:Debug>:/showIncludes>		
	)

	#only target our project
	target_compile_options(
		${CMAKE_PROJECT_NAME}
		PRIVATE

		#disable warnings for struct padding and spectre mitigation wuen WX is enabled
		$<$<CONFIG:Debug>:/wd5045>
		$<$<CONFIG:Debug>:/wd4820>
		$<$<CONFIG:Debug>:/wd4574>

		#for debug configs
		$<$<CONFIG:Debug>:/options:strict>
		#disable warnings for struct padding and spectre mitigation wuen WX is enabled
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

#include mimalloc headers
include_directories(../mimalloc/include)

if(MSVC)

	#find the mimalloc static library for windows builds
	find_library(VNLIB_MIMALLOC_DEBUG_LIB
		NAMES mimalloc-static
		PATHS ../mimalloc/build/Debug
	)

	find_library(VNLIB_MIMALLOC_RELEASE_LIB
		NAMES mimalloc-static
		PATHS ../mimalloc/build/Release
	)

	#target static libraries for all configs
	target_link_libraries(${CMAKE_PROJECT_NAME} 
		$<$<CONFIG:Debug>:${VNLIB_MIMALLOC_DEBUG_LIB}>
		#set release for all release configs
		$<$<CONFIG:MinSizeRel>:${VNLIB_MIMALLOC_RELEASE_LIB}>
		$<$<CONFIG:RelWithDepInfo>:${VNLIB_MIMALLOC_RELEASE_LIB}>
		$<$<CONFIG:Release>:${VNLIB_MIMALLOC_RELEASE_LIB}>
	)

elseif(UNIX)

	#find the mimalloc library for unix builds
	find_library(VNLIB_MIMALLOC_LIB
		NAMES mimalloc
		PATHS ../mimalloc/build
	)

	#add the mimalloc library to the project
	target_link_libraries(${CMAKE_PROJECT_NAME} ${VNLIB_MIMALLOC_LIB})

else()

	#failure 
	message(FATAL_ERROR "Unsupported platform configuration for mimalloc library")

endif()	