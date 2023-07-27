cmake_minimum_required(VERSION 3.0)

project(vnlib_compress)

#export header files to the main project
file(GLOB COMP_HEADERS *.h)

#Add indepednent source files to the project
set(VNLIB_COMPRESS_SOURCES compression.c)

#add feature specific source files to the project
if(ENABLE_BROTLI)
	set(VNLIB_FEATURE_BR_SOURCES feature_brotli.c)
endif()

if(ENABLE_ZLIB)
	set(VNLIB_FEATURE_ZLIB_SOURCES feature_zlib.c)
endif()

#create my shared library
add_library(${CMAKE_PROJECT_NAME} SHARED ${VNLIB_COMPRESS_SOURCES} ${COMP_HEADERS} ${VNLIB_FEATURE_BR_SOURCES} ${VNLIB_FEATURE_ZLIB_SOURCES})

#Setup the compiler options 

enable_language(C)
set(CMAKE_CXX_STANDARD 90)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

#enable position independent code (for shared libraries with exports)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

#strict error checking for main project
set(CMAKE_COMPILE_WARNING_AS_ERROR ON)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

#force the compiler to use the C90/89 standard
target_compile_features(${CMAKE_PROJECT_NAME} PRIVATE c_std_90)

#setup flags for windows compilation
if(MSVC)
	
    #global windows cl flags
	add_compile_options(
		/Qspectre 
		/sdl
		/TC
		/GS 

		#for debug configs		
		$<$<CONFIG:Debug>:/Wall>
		$<$<CONFIG:Debug>:/options:strict>
		$<$<CONFIG:Debug>:/FC>
		$<$<CONFIG:Debug>:/showIncludes>		
	)

	#only target our project
	target_compile_options(
		${CMAKE_PROJECT_NAME}
		PRIVATE

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
		${CMAKE_PROJECT_NAME} 
		PUBLIC 
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
		$<$<CONFIG:Debug>:-pedantic>
	)

endif()

#check for brotli feature enablement
if(ENABLE_BROTLI)	
	#add the include directory for brotli so we can include the header files
	include_directories(../third-party/brotli/c/include)

	#get common sources
	file(GLOB BROTLI_SOURCES ../third-party/brotli/c/common/*.c)

	#we need to add the brotli encoder source files to the project
	file(GLOB BROTLI_ENC_SOURCES ../third-party/brotli/c/enc/*.c)

	#add brotli as a static library to link later
	add_library(lib_brotli STATIC ${BROTLI_SOURCES} ${BROTLI_ENC_SOURCES})

	#define the brotli feature macro to enable brotli support
	add_definitions(-DVNLIB_COMPRESSOR_BROTLI_ENABLED)
	
	target_link_libraries(${CMAKE_PROJECT_NAME} lib_brotli)
endif()

#check for zlib feature enablement
if(ENABLE_ZLIB)	
	#add the include directory for zlib so we can include the header files
	include_directories(../third-party/zlib)

	#we only need to add the zlib deflate source files to the project
	set(ZLIB_SOURCES 
		../third-party/zlib/deflate.c
		../third-party/zlib/adler32.c
		../third-party/zlib/adler32_simd.c
		../third-party/zlib/crc32.c
		../third-party/zlib/zutil.c
		../third-party/zlib/trees.c
	)

	#add zlib as a library to link later
	add_library(lib_deflate STATIC ${ZLIB_SOURCES})

	#define the zlib feature macro to enable zlib support
	add_definitions(-DVNLIB_COMPRESSOR_ZLIB_ENABLED)
	
	target_link_libraries(${CMAKE_PROJECT_NAME} lib_deflate)
endif()

