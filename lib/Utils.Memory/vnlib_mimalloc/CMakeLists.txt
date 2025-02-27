cmake_minimum_required(VERSION 3.18)

project(vnlib_mimalloc C)
set(_MI_PROJ_NAME "vnlib_mimalloc")

option(ENABLE_GREEDY "Enable greedy allocator configuration" ON)
option(ENABLE_STATIC_FPIC "Enable fPIC for static library" ON)
set(CMAKE_BUILD_TYPE "Release" CACHE STRING "The build configuration type")

string(TOLOWER ${CMAKE_BUILD_TYPE} build_type)
message(STATUS "Build type is '${build_type}'")

#Setup the compiler options 
set(CMAKE_C_STANDARD 11)					#c11 is required for mimalloc static assertions
set(CMAKE_C_STANDARD_REQUIRED ON)

#Add all project headers and source files
set(VNLIB_MIMALLOC_SOURCES
	"NativeHeapApi.h"
	"vnlib_mimalloc.c"
)

#create my shared library
add_library(${_MI_PROJ_NAME} SHARED ${VNLIB_MIMALLOC_SOURCES})
add_library(${_MI_PROJ_NAME}_static STATIC ${VNLIB_MIMALLOC_SOURCES})
#enable fPIC for shared library
set_target_properties(${_MI_PROJ_NAME} PROPERTIES POSITION_INDEPENDENT_CODE ON)

if (ENABLE_STATIC_FPIC)
	set_target_properties(${_MI_PROJ_NAME}_static PROPERTIES POSITION_INDEPENDENT_CODE ON)
	message(STATUS "Static library will be built with fPIC. Set -DENABLE_STATIC_FPIC=OFF to disable this feature")
endif()

#if on unix lib will be appended, so we can adjust
if(UNIX)
	set_target_properties(
		${_MI_PROJ_NAME} 
		${_MI_PROJ_NAME}_static 
		
		PROPERTIES 
		OUTPUT_NAME 
		vn_mimalloc
	)
endif()

set(_MI_COMP_ARGS)
set(_MI_COMP_DEFS)

#setup flags for windows compilation
if(MSVC)

	#only target our project
	list(APPEND _MI_COMP_ARGS
		/Qspectre 
		/sdl
		/TC
		/GS 

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
		$<$<CONFIG:Debug>:/FC>
		$<$<CONFIG:Debug>:/showIncludes>		
	)

	#set build macros
	list(APPEND _MI_COMP_DEFS
		$<$<CONFIG:DEBUG>:DEBUG>
		$<$<CONFIG:RELEASE>:RELEASE>
	)

#configure gcc flags
elseif(CMAKE_COMPILER_IS_GNUCC)

	list(APPEND _MI_COMP_ARGS
		-Wextra
		-fstack-protector
	)

	#enable debug compiler options
	if(build_type STREQUAL "debug")
		list(APPEND _MI_COMP_ARGS
			-g				#enable debugger info
			-Og				#disable optimizations
			-Wall			#enable all warnings
			-Werror			#treat warnings as errors
			-pedantic		#enable pedantic mode
		)

		list(APPEND _MI_COMP_DEFS 
			DEBUG
		)

	endif()

else()
	message(FATAL_ERROR "Unsupported compiler, sorry. Submit an issue for your platform and I'll work on it :)")
endif()

#add the definitions to the project
target_compile_definitions(${_MI_PROJ_NAME} PRIVATE ${_MI_COMP_DEFS})
target_compile_definitions(${_MI_PROJ_NAME}_static PRIVATE ${_MI_COMP_DEFS})

#add the compiler flags to the project
target_compile_options(${_MI_PROJ_NAME} PRIVATE ${_MI_COMP_ARGS})
target_compile_options(${_MI_PROJ_NAME}_static PRIVATE ${_MI_COMP_ARGS})

#include mimalloc headers
include_directories(vendor/include)

##################################
#								 #
#	Include mimalloc library	 #
#								 #
##################################

set(MI_BUILD_TESTS OFF)
set(MI_BUILD_STATIC ON)
set(MI_BUILD_SHARED OFF)
set(MI_BUILD_OBJECT OFF)
set(MI_BUILD_TESTS OFF)
set(MI_XMALLOC OFF)			#Do not allow process abort when alloc fails, we don't need it 
set(MI_OVERRIDE OFF)
set(MI_WIN_REDIRECT OFF)

add_subdirectory(
	${CMAKE_CURRENT_SOURCE_DIR}/vendor
	${CMAKE_CURRENT_BINARY_DIR}/mimalloc
)

target_link_libraries(${_MI_PROJ_NAME} PRIVATE mimalloc-static)
target_link_libraries(${_MI_PROJ_NAME}_static PRIVATE mimalloc-static)

