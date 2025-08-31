cmake_minimum_required(VERSION 3.18)

project(
	vnlib_rpmalloc 
	LANGUAGES C
	DESCRIPTION "Exposes the rpmalloc allocator to the NativeHeapApi compatible with vnlib.core"
	HOMEPAGE_URL "https://www.vaughnnugent.com/resources/modules/vnlib.core"
)

set(_RP_PROJ_NAME "vnlib_rpmalloc")

option(ENABLE_GREEDY "Enable greedy allocator configuration" ON)
option(ENABLE_STATIC_FPIC "Enable fPIC for static library" ON)
set(CMAKE_BUILD_TYPE "Release" CACHE STRING "The build configuration type")

string(TOLOWER ${CMAKE_BUILD_TYPE} build_type)
message(STATUS "Build type is '${build_type}'")

# Setup the compiler options 
set(CMAKE_C_STANDARD 11)					# c11 is required for rpmalloc static assertions
set(CMAKE_C_STANDARD_REQUIRED ON)

# Add independent source files to the project
set(VNLIB_RPMALLOC_SOURCES 
    "NativeHeapApi.h"
	"vnlib_rpmalloc.c" 
    "vendor/rpmalloc.h"
	"vendor/rpmalloc.c"
)

# Add rpmalloc includes, there will only be one library
include_directories(vendor)

# Create shared/static libs
add_library(${_RP_PROJ_NAME} SHARED ${VNLIB_RPMALLOC_SOURCES})
add_library(${_RP_PROJ_NAME}_static STATIC ${VNLIB_RPMALLOC_SOURCES})
# Enable fPIC for shared library
set_target_properties(${_RP_PROJ_NAME} PROPERTIES POSITION_INDEPENDENT_CODE ON)

if (ENABLE_STATIC_FPIC)
	set_target_properties(${_RP_PROJ_NAME}_static PROPERTIES POSITION_INDEPENDENT_CODE ON)
	message(STATUS "Static library will be built with fPIC. Set -DENABLE_STATIC_FPIC=OFF to disable this feature")
endif()

# If on unix lib will be appended, so we can adjust
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

# Setup flags for windows compilation
if(MSVC)

	list(APPEND _RP_COMP_ARGS
		/Qspectre 
		/sdl
		/TC
		/GS 
		/std:c11  # enable C11 support
		/experimental:c11atomics

		# Disable warnings for struct padding and spectre mitigation when WX is enabled
		$<$<CONFIG:Debug>:/wd5045>
		$<$<CONFIG:Debug>:/wd4820>
		$<$<CONFIG:Debug>:/wd4574>		

		# For debug configs
		$<$<CONFIG:Debug>:/options:strict>
		# Disable warnings for struct padding and spectre mitigation when WX is enabled
		$<$<CONFIG:Debug>:/Wall>
		$<$<CONFIG:Debug>:/WX>		# Warnings as errors (only for our project)
		$<$<CONFIG:Debug>:/Zi>		# Enable debug info
		$<$<CONFIG:Debug>:/Zo>	
		$<$<CONFIG:Debug>:/FC>		# Full path in diagnostics
		$<$<CONFIG:Debug>:/showIncludes>
	)

	list(APPEND _RP_COMP_DEFS
		$<$<CONFIG:DEBUG>:DEBUG>
		$<$<CONFIG:RELEASE>:RELEASE>
	)

# Configure gcc flags
elseif(CMAKE_COMPILER_IS_GNUCC)

	list(APPEND _RP_COMP_ARGS
		-Wextra
		-fstack-protector
	)

	# Enable debug compiler options
	if(build_type STREQUAL "debug")
		list(APPEND _RP_COMP_ARGS
			-g				# Enable debugger info
			-Og				# Disable optimizations
			-Wall			# Enable all warnings
			-Werror			# Treat warnings as errors
			-pedantic		# Enable pedantic mode
		)
	endif()

else()
	message(FATAL_ERROR "Unsupported compiler, sorry. Submit an issue for your platform and I'll work on it :)")
endif()

# Enable required features
list(APPEND _RP_COMP_DEFS
	RPMALLOC_FIRST_CLASS_HEAPS=1
	ENABLE_OVERRIDE=0			# Disable malloc override, not needed for our uses
	MAX_ALLOC_SIZE=999999999

	# Add some debugging/tracing for debug mode
	$<$<CONFIG:Debug>:ENABLE_TRACE=1>
	$<$<CONFIG:Debug>:ENABLE_VALIDATE_ARGS=1>
)

if(ENABLE_GREEDY)

	list(APPEND _RP_COMP_DEFS
		# If greedy is enabled, add greedy options
		ENABLE_UNLIMITED_CACHE=1
		# On by default but we otherwise disable global cache to really reduce committed size
		ENABLE_GLOBAL_CACHE=1
		ENABLE_UNMAP
	)

else()

	list(APPEND _RP_COMP_DEFS
		# Disable greedy definitions
		ENABLE_UNLIMITED_CACHE=0
		ENABLE_GLOBAL_CACHE=0
		DISABLE_UNMAP=0					# Allow unmapping of pages during free instead of global cache
	)

endif()

# Add the definitions to the project
target_compile_definitions(${_RP_PROJ_NAME} PRIVATE ${_RP_COMP_DEFS})
target_compile_definitions(${_RP_PROJ_NAME}_static PRIVATE ${_RP_COMP_DEFS})

# Add the compiler flags to the project
target_compile_options(${_RP_PROJ_NAME} PRIVATE ${_RP_COMP_ARGS})
target_compile_options(${_RP_PROJ_NAME}_static PRIVATE ${_RP_COMP_ARGS})