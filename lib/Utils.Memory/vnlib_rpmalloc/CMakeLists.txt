cmake_minimum_required(VERSION 3.10)

project(vnlib_rpmalloc C)
set(_RP_PROJ_NAME "vnlib_rpmalloc")

option(ENABLE_GREEDY "Enable greedy allocator configuration" ON)
set(CMAKE_BUILD_TYPE "Release" CACHE STRING "The build configuration type")

string(TOLOWER ${CMAKE_BUILD_TYPE} build_type)
message(STATUS "Build type is '${build_type}'")

#Setup the compiler options 
set(CMAKE_C_STANDARD 11)					#c11 is required for rpmalloc static assertions
set(CMAKE_C_STANDARD_REQUIRED ON)

#Add indepednent source files to the project
set(VNLIB_RPMALLOC_SOURCES 
    "NativeHeapApi.h"
	"vnlib_rpmalloc.c" 
    "vendor/rpmalloc.h"
	"vendor/rpmalloc.c"
)

#add rpmalloc includes, there will only be one library
include_directories(vendor)

#create shared/static libs
add_library(${_RP_PROJ_NAME} SHARED ${VNLIB_RPMALLOC_SOURCES})
add_library(${_RP_PROJ_NAME}_static STATIC ${VNLIB_RPMALLOC_SOURCES})
#enable fPIC for shared library
set_target_properties(${_RP_PROJ_NAME} PROPERTIES POSITION_INDEPENDENT_CODE ON)

#if on unix lib will be appended, so we can adjust
if(UNIX)
	set_target_properties(
		${_RP_PROJ_NAME} ${_RP_PROJ_NAME}_static 
		
		PROPERTIES 
		OUTPUT_NAME 
		vn_rpmalloc
	)
endif()

set(_RP_COMP_ARGS)
set(_RP_COMP_DEFS)

#setup flags for windows compilation
if(MSVC)

	list(APPEND _RP_COMP_ARGS
		/Qspectre 
		/sdl
		/TC
		/GS 

		#disable warnings for struct padding and spectre mitigation when WX is enabled
		$<$<CONFIG:Debug>:/wd5045>
		$<$<CONFIG:Debug>:/wd4820>
		$<$<CONFIG:Debug>:/wd4574>

		#for debug configs
		$<$<CONFIG:Debug>:/options:strict>
		#disable warnings for struct padding and spectre mitigation wuen WX is enabled
		$<$<CONFIG:Debug>:/Wall>
		$<$<CONFIG:Debug>:/WX>		#warnings as errors (only for our project)
		$<$<CONFIG:Debug>:/Zi>		#enable debug info
		$<$<CONFIG:Debug>:/Zo>	
		$<$<CONFIG:Debug>:/FC>		#full path in diagnostics
		$<$<CONFIG:Debug>:/showIncludes>
	)

	list(APPEND _RP_COMP_DEFS
		$<$<CONFIG:DEBUG>:DEBUG>
		$<$<CONFIG:RELEASE>:RELEASE>
	)

#configure gcc flags
elseif(CMAKE_COMPILER_IS_GNUCC)

	list(APPEND _RP_COMP_ARGS
		-Wextra
		-fstack-protector
	)

	#enable debug compiler options
	if(build_type STREQUAL "debug")
		list(APPEND _RP_COMP_ARGS
			-g				#enable debugger info
			-Og				#disable optimizations
			-Wall			#enable all warnings
			-Werror			#treat warnings as errors
			-pedantic		#enable pedantic mode
		)
	endif()

else()
	message(FATAL_ERROR "Unsupported compiler, sorry. Submit an issue for your platform and I'll work on it :)")
endif()

#enable required features
list(APPEND _RP_COMP_DEFS
	RPMALLOC_FIRST_CLASS_HEAPS=1
	ENABLE_ADAPTIVE_THREAD_CACHE=1
	ENABLE_THREAD_CACHE=1		#always enable per-thred cache
	ENABLE_OVERRIDE=0			#disable malloc override, not needed for our uses

	#add some debugging/tracing for debug mode
	$<$<CONFIG:Debug>:ENABLE_TRACE=1>
	$<$<CONFIG:Debug>:ENABLE_VALIDATE_ARGS=1>
)

if(ENABLE_GREEDY)

	list(APPEND _RP_COMP_DEFS
		#if greedy is enabled, add greedy options
		ENABLE_UNLIMITED_CACHE=1
		#On by default but we otherwise disable global cache to really reduce commited size
		ENABLE_GLOBAL_CACHE=1
	)

else()

	list(APPEND _RP_COMP_DEFS
		#disable greedy definitions
		ENABLE_UNLIMITED_CACHE=0
		ENABLE_GLOBAL_CACHE=0
		DISABLE_UNMAP=0					#allow unmapping of pages during free instead of global cache
	)

endif()

#add the definitions to the project
target_compile_definitions(${_RP_PROJ_NAME} PRIVATE ${_RP_COMP_DEFS})
target_compile_definitions(${_RP_PROJ_NAME}_static PRIVATE ${_RP_COMP_DEFS})

#add the compiler flags to the project
target_compile_options(${_RP_PROJ_NAME} PRIVATE ${_RP_COMP_ARGS})
target_compile_options(${_RP_PROJ_NAME}_static PRIVATE ${_RP_COMP_ARGS})