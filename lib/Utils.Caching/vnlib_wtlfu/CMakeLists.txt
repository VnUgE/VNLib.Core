# Copyright (c) 2026 Vaughn Nugent
# See the LICENSE in this directory for terms of use
#
# CMake configuration for vnlib_wtlfu - W-TinyLFU LRU cache library

cmake_minimum_required(VERSION 3.22)

project(
	vnlib_wtlfu
	LANGUAGES C
	DESCRIPTION "W-TinyLFU LRU cache with Count-Min Sketch admission policy"
	HOMEPAGE_URL "https://www.vaughnnugent.com/resources/software/modules/vnlib_wtlfu"
	VERSION ${CMAKE_PROJECT_VERSION}
)

set(_WTL_PROJ_NAME "vnlib_wtlfu")

option(VNLIB_BUILD_TESTS "Build tests" OFF)

string(TOLOWER ${CMAKE_BUILD_TYPE} build_type)

#-----------------------------
#		MAIN PROJECT
#-----------------------------

# Library source files — only compiled translation units go here.
# Internal headers are listed so they appear in IDEs but are not
# installed or distributed with the public API.
set(WTLFU_SRCS
	"src/hash.c"
	"src/cmsketch.c"
	"src/lru.c"
	"src/hashtable.c"
	"src/cache.c"
)

set(WTLFU_HEADERS
	"include/wtlfu.h"
	"include/platform.h"
	"src/debug.h"
	"src/span.h"
	"src/hash.h"
	"src/lru.h"
	"src/cmsketch.h"
	"src/internal.h"
	"src/hashtable.h"	
	"src/cache.h"
)

# Build both shared and static variants from the same sources.
add_library(${_WTL_PROJ_NAME} SHARED ${WTLFU_SRCS} ${WTLFU_HEADERS})
add_library(${_WTL_PROJ_NAME}_static STATIC ${WTLFU_SRCS} ${WTLFU_HEADERS})

# Renames the static library output file to avoid collisions on Windows systems
set_target_properties(${_WTL_PROJ_NAME}_static PROPERTIES OUTPUT_NAME ${_WTL_PROJ_NAME}_static)

# Set C99 standard across both targets. C_EXTENSIONS is OFF to keep
# the code portable and avoid GNU/MS extensions leaking in for now.
# also enables fpic for both builds for consumer sanity sake
set_target_properties(
	${_WTL_PROJ_NAME}
	${_WTL_PROJ_NAME}_static
	PROPERTIES
	C_STANDARD 99
	C_STANDARD_REQUIRED ON
	C_EXTENSIONS OFF
	POSITION_INDEPENDENT_CODE ON
)

target_compile_features(${_WTL_PROJ_NAME} PRIVATE c_std_99)
target_compile_features(${_WTL_PROJ_NAME}_static PRIVATE c_std_99)

# Private include paths: "include/" for the public API header,
# "src/" for internal headers used by implementation files.
target_include_directories(${_WTL_PROJ_NAME} PRIVATE include PRIVATE src)
target_include_directories(${_WTL_PROJ_NAME}_static PRIVATE include PRIVATE src)

target_compile_definitions(${_WTL_PROJ_NAME} PRIVATE 
	$<$<CONFIG:Debug>:DEBUG>
	VNLIB_EXPORTING=1
	WTL_VERSION_STRING="${CMAKE_PROJECT_VERSION}"
)
target_compile_definitions(${_WTL_PROJ_NAME}_static PRIVATE 
	$<$<CONFIG:Debug>:DEBUG>
	VNLIB_EXPORTING=1
	WTL_VERSION_STRING="${CMAKE_PROJECT_VERSION}"
)

# Treat all warnings as errors so regressions are caught early.
set(CMAKE_COMPILE_WARNING_AS_ERROR ON)

set(_WTL_COMPILE_OPTS)

#-----------------------------
#		Compiler flags
#-----------------------------

# MSVC: security checks and strict C compilation mode.
# Debug config adds full diagnostics, all warnings, and rich debug info.
if(MSVC)

	list(APPEND _WTL_COMPILE_OPTS
		/sdl						# enable additional security checks
		/TC							# compile as c (not cpp)
		/GS							# buffer security check
	)

	if(build_type STREQUAL "debug")
		list(APPEND _WTL_COMPILE_OPTS
			/FC						# show full path in diagnostics
			/showIncludes			# show included header files during build

			/wd4820					# disable padding warnings
			/wd5045					# disable spectre mitigation warnings

			/options:strict			# strict compiler options
			/Wall					# enable all warnings
			/WX						# forces warnings as errors
			/Zi						# enable rich debug info
			/Zo						# enhanced debug info by linking to the debug msvc library
		)
	endif()

# GCC/Clang: extra warnings and stack protection.
# Debug config adds pedantic warnings and strict prototype checks.
elseif(CMAKE_COMPILER_IS_GNUCC)

	list(APPEND _WTL_COMPILE_OPTS
		-Wextra 
		-fstack-protector
	)

	if(build_type STREQUAL "debug")
		list(APPEND _WTL_COMPILE_OPTS
			-g						# enable debug info
			-Og						# disable optimizations
			-Wall					# enable all warnings
			-pedantic				# enable pedantic warnings
			-Winit-self				#warn about self initialization
			-Wcast-qual
			-Wpointer-arith
			-Wstrict-prototypes
			-Wswitch-default
			-Wshadow				# warn when a local variable shadows another
			-Wformat=2				# warn about format strings
			-Wundef					# warn about undefined macros
			-Wcast-align			# warn about pointer casts that increase alignment
			-Wmissing-prototypes	# warn if global function defined without prototype
			-Wmissing-declarations	# warn if global function defined without declaration
			-Wno-unused-function	# warn of unused function
		)
	endif()

endif()

# Apply the selected compiler flags to both library targets.
target_compile_options(${_WTL_PROJ_NAME} PRIVATE ${_WTL_COMPILE_OPTS})
target_compile_options(${_WTL_PROJ_NAME}_static PRIVATE ${_WTL_COMPILE_OPTS})

###########################
#
#		TESTS
#
###########################

if(VNLIB_BUILD_TESTS)

	enable_testing()

# Test helper: creates an executable linked against the shared library
# by default, or the static library if STATIC is given.
	function(add_wtl_test TEST_NAME)
		cmake_parse_arguments(ARG "" "" "SOURCES;INCLUDES;STATIC" ${ARGN})

		# always add the test-base.c file 
		add_executable(${TEST_NAME} ${ARG_SOURCES})

		if (MSVC)
			target_link_libraries(${TEST_NAME} bcrypt.lib)
		endif()

		if(ARG_STATIC)
			target_link_libraries(${TEST_NAME} ${_WTL_PROJ_NAME}_static)
		else()
			target_link_libraries(${TEST_NAME} ${_WTL_PROJ_NAME})
		endif()

		# include all internal headers and test headers automatically
		target_include_directories(
			${TEST_NAME} 
			PRIVATE include 
			PRIVATE src ${ARG_INCLUDES} 
			PRIVATE tests/include
		)

		target_compile_features(${TEST_NAME} PRIVATE c_std_99)
		target_compile_definitions(${TEST_NAME} PRIVATE VNLIB_EXPORTING)

		add_test(
			NAME ${TEST_NAME}
			COMMAND ${TEST_NAME}
			CONFIGURATIONS ${CMAKE_BUILD_TYPE}
		)
	endfunction()

	# In debug/test builds, internal symbols are exported from the
	# shared library via the _VN_WTLFU_INTERNAL macro, so the test
	# can link against the DLL like any other consumer.

	add_wtl_test(WtlHashTest SOURCES tests/test-base.c tests/hash/main.c)

	add_wtl_test(WtlSketchTest SOURCES tests/test-base.c tests/cmsketch/main.c)

	# the lru unit tests
	add_wtl_test(WtlLruTest SOURCES tests/test-base.c tests/lru/main.c)

	# hashtable tests
	add_wtl_test(WtlHashTableTest SOURCES tests/test-base.c tests/hashtable/main.c)

	add_wtl_test(WtlCacheTests SOURCES tests/test-base.c tests/cache/main.c)

endif()

# TODO: Install rules (vnlib core native libraries do not install via CMake)

message("\n${_WTL_PROJ_NAME} project summary")
message("=====================================")
message(" -- Version: ${CMAKE_PROJECT_VERSION} --")
message(" -- Configuration: ${build_type} --")
message("  Source dir: ........ ${CMAKE_SOURCE_DIR}")
message("  Binary dir: ........ ${CMAKE_BINARY_DIR}")
message("  Test Status: ....... ${VNLIB_BUILD_TESTS}")
message("=====================================")
