cmake_minimum_required(VERSION 3.10)

project(vnlib_mimalloc C)
set(CMAKE_PROJECT_NAME "vnlib_mimalloc")

option(ENABLE_GREEDY "Enable greedy allocator configuration" ON)
set(CMAKE_BUILD_TYPE "Release" CACHE STRING "The build configuration type")

#Setup the compiler options 
set(CMAKE_C_STANDARD 11)					#c11 is required for mimalloc static assertions
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)		#enable position independent code (for shared libraries with exports)

string(TOLOWER ${CMAKE_BUILD_TYPE} build_type)
message(STATUS "Build type is '${build_type}'")

#export header files to the main project
file(GLOB HEADERS *.h)

#Add indepednent source files to the project
file(GLOB VNLIB_MIMALLOC_SOURCES *.c)

#create my shared library
add_library(${CMAKE_PROJECT_NAME} SHARED ${VNLIB_MIMALLOC_SOURCES} ${HEADERS})
add_library(${CMAKE_PROJECT_NAME}_static STATIC ${VNLIB_MIMALLOC_SOURCES} ${HEADERS})

#if on unix lib will be appended, so we can adjust
if(UNIX)
	set_target_properties(${CMAKE_PROJECT_NAME} ${CMAKE_PROJECT_NAME}_static PROPERTIES OUTPUT_NAME vn_mimalloc)
endif()

#setup flags for windows compilation
if(MSVC)

	#if debug
	target_compile_definitions(${CMAKE_PROJECT_NAME} PRIVATE $<$<CONFIG:Debug>:DEBUG>)

	#only target our project
	target_compile_options(
		${CMAKE_PROJECT_NAME}
		PRIVATE

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
	target_compile_definitions( 
		${CMAKE_PROJECT_NAME}
		PRIVATE

		$<$<CONFIG:DEBUG>:DEBUG>
		$<$<CONFIG:RELEASE>:RELEASE>
	)

#configure gcc flags
elseif(CMAKE_COMPILER_IS_GNUCC)

	target_compile_options(
		${CMAKE_PROJECT_NAME}
		PRIVATE

		-Wextra
		-fstack-protector
	)

	#enable debug compiler options
	if(build_type STREQUAL "debug")
		target_compile_options(
			${CMAKE_PROJECT_NAME}
			PRIVATE

			-g				#enable debugger info
			-Og				#disable optimizations
			-Wall			#enable all warnings
			-Werror			#treat warnings as errors
			-pedantic		#enable pedantic mode
		)

		target_compile_definitions(${CMAKE_PROJECT_NAME} PRIVATE DEBUG)

	endif()

else()
	message(FATAL_ERROR "Unsupported compiler, sorry. Submit an issue for your platform and I'll work on it :)")
endif()


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
set(MI_OVERRIDE OFF)
set(MI_WIN_REDIRECT OFF)

add_subdirectory(
	${CMAKE_CURRENT_SOURCE_DIR}/vendor
	${CMAKE_CURRENT_BINARY_DIR}/mimalloc
)

target_link_libraries(${CMAKE_PROJECT_NAME} PRIVATE mimalloc-static)
target_link_libraries(${CMAKE_PROJECT_NAME}_static PRIVATE mimalloc-static)
