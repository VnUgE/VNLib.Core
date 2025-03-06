cmake_minimum_required(VERSION 3.18)

project(
	vnlib_monocypher 
	LANGUAGES C
	DESCRIPTION "CMake build for vnlib_monocypher portable cryptographic software"
	HOMEPAGE_URL "https://www.vaughnnugent.com/resources/modules/vnlib.core"
)

set(_PROJECT_NAME ${CMAKE_PROJECT_NAME})

string(TOLOWER ${CMAKE_BUILD_TYPE} build_type)
message(STATUS "Build type is '${build_type}'")

#include all the source files in the src directory
file(GLOB VNLIB_MONOCYPHER_SOURCES src/*)

#add monocypher includes, there will only be one library
include_directories(vendor/src)
list(
	APPEND 
	VNLIB_MONOCYPHER_SOURCES 
	vendor/src/monocypher.h
	vendor/src/monocypher.c
)

#Setup the compiler options for C99
set(CMAKE_C_STANDARD 99)
set(CMAKE_C_STANDARD_REQUIRED ON)
#enable position independent code (for shared libraries with exports)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

#create my shared library
add_library(${_PROJECT_NAME} SHARED ${VNLIB_MONOCYPHER_SOURCES})
add_library(${_PROJECT_NAME}_static STATIC ${VNLIB_MONOCYPHER_SOURCES})

#if on unix lib will be appended, so we can adjust
if(UNIX)
	set_target_properties(${_PROJECT_NAME} ${_PROJECT_NAME}_static PROPERTIES OUTPUT_NAME vn_monocypher)
endif()

target_compile_features(${_PROJECT_NAME} PRIVATE c_std_99)		#force compiler to use c90 standard for library

add_compile_definitions(VNLIB_EXPORTING)

###############################
#		* NOTE! [Build Security] *
# Monocypher is included as source code since  it does not supoort cross platform 
# compilation without intervention. Since it's a strict c99 project the source 
# should allow it to compile, but he does not include any build scripts to do 
# it. So I have to pull it in as source code and CAREFULLY follow the build
# flags to match the makefile flags, so I don't add any insecurities to the
# library.
###############################


#setup flags for windows compilation
if(MSVC)
	
	target_compile_options(
		${_PROJECT_NAME}
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
		${_PROJECT_NAME}
		PRIVATE

		$<$<CONFIG:DEBUG>:DEBUG>
		$<$<CONFIG:RELEASE>:RELEASE>
	)

#configure gcc flags
elseif(CMAKE_COMPILER_IS_GNUCC)

	target_compile_options(
		${_PROJECT_NAME}
		PRIVATE

		-Wextra
		-fstack-protector
	)

	#enable debug compiler options
	if(build_type STREQUAL "debug")
		target_compile_options(
			${_PROJECT_NAME}
			PRIVATE

			-std=c99		#force c99 standard
			-g				#enable debugger info
			-Og				#disable optimizations
			-Wall			#enable all warnings
			-Werror			#treat warnings as errors
			-pedantic		#enable pedantic mode
		)

		target_compile_definitions(${_PROJECT_NAME} PRIVATE DEBUG)

	endif()

	if(build_type STREQUAL "release")
		target_compile_options(
			${_PROJECT_NAME}
			PRIVATE

			-std=c99		#force c99 standard
			-O3				#enable max optimizations
			-Werror			#treat warnings as errors
		)

		target_compile_definitions(${_PROJECT_NAME} PRIVATE DEBUG)

	endif()

else()
	message(FATAL_ERROR "Unsupported compiler, sorry. Submit an issue for your platform and I'll work on it :)")
endif()
