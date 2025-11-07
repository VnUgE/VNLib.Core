#! /bin/bash

# Setup script for AMD64 build environment for apt and dnf based systems

set -o pipefail
set -e

export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

# Ensure dotnet is in PATH
export DOTNET_ROOT=$(pwd)/.dotnet
export PATH="$PATH:${DOTNET_ROOT}:$HOME/.dotnet/tools"

# ============================================================================
# CONFIGURATION SECTION - Update URLs, checksums, and versions here
# ============================================================================

# GoTask installation
GOTASK_TAR_URL="https://github.com/go-task/task/releases/download/v3.45.4/task_linux_amd64.tar.gz"
GOTASK_CHECKSUM="4367eba04abcbcb407578d18d2439ee32604a872419601abec76a829c797fb82"

# vnbuild installation
VNBUILD_URL="https://www.vaughnnugent.com/public/resources/software/builds/vnbuild/269c8d946a4826fcb3d06f4e5159ca6e2fab74de/vnbuild/linux-x64.tgz"
VNBUILD_CHECKSUM="d2e58c679929eb0312acda26a1ee0547190b8f00438a5d5cda2924291793b327"

# install cmake from a tarball (avoid old versions in apt/dnf)
CMAKE_URL="https://github.com/Kitware/CMake/releases/download/v4.1.2/cmake-4.1.2-linux-x86_64.tar.gz"
CMAKE_CHECKSUM="773cc679c3a7395413bd096523f8e5d6c39f8718af4e12eb4e4195f72f35e4ab"

DOTNET_URL="https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.415/dotnet-sdk-8.0.415-linux-x64.tar.gz"
DOTNET_CHECKSUM="0fc0499a857f161f7c35775bb3f50ac6f0333f02f5df21d21147d538eb26a9a87282d4ba3707181c46f3c09d22cdc984e77820a5953a773525d6f7b332deb7f2"

# GitVersion tool
GITVERSION_VERSION="6.3.0"

log_info() {
    echo "[INFO] $*"
}

log_error() {
    echo "[ERROR] $*" >&2
}

log_success() {
    echo "[SUCCESS] $*"
}

# Detect OS type
detect_os() {
    if [ -f /etc/debian_version ]; then
        echo "debian"
    elif [ -f /etc/redhat-release ]; then
        echo "redhat"
    else
        log_error "Unsupported OS. Only Debian/Ubuntu and RedHat/Fedora/Alma are supported."
        exit 1
    fi
}

# Check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Verify checksum of a file
verify_checksum() {
    local file=$1
    local expected_checksum=$2
    
    if [ "${expected_checksum}" = "PLACEHOLDER_UPDATE_THIS" ]; then
        log_info "Skipping checksum verification for ${file} (placeholder checksum)"
        return 0
    fi

    # compute actual checksum
    local actual_checksum=""

    if [ ${#expected_checksum} -eq 128 ]; then
        # SHA512
        actual_checksum=$(sha512sum "${file}" | awk '{print $1}')
    elif [ ${#expected_checksum} -eq 64 ]; then
        # SHA256
        actual_checksum=$(sha256sum "${file}" | awk '{print $1}')
    elif [ ${#expected_checksum} -eq 40 ]; then
        # SHA1
        actual_checksum=$(sha1sum "${file}" | awk '{print $1}')
    elif [ ${#expected_checksum} -eq 32 ]; then
        # MD5
        actual_checksum=$(md5sum "${file}" | awk '{print $1}')
    else
        log_error "Unable to determine checksum type for expected checksum: ${expected_checksum}"
        return 1
    fi

    if [ "${actual_checksum}" != "${expected_checksum}" ]; then
        log_error "Checksum verification failed for ${file}"
        log_error "Expected: ${expected_checksum}"
        log_error "Actual:   ${actual_checksum}"
        return 1
    fi
    
    log_success "Checksum verified for ${file}"
    return 0
}

install_tarball() {
    local url=$1
    local checksum=$2
    local install_dir=$3
    
    local tarball_name=$(basename "${url}")

    log_info "Downloading ${tarball_name}..."

    curl -fsSL "${url}" -o "${tarball_name}.tgz"
    verify_checksum "${tarball_name}.tgz" "${checksum}" || exit 1

    log_info "Installing ${tarball_name}..."

    mkdir -p "${install_dir}"
    tar -xzf "${tarball_name}.tgz" -C "${install_dir}"
}

install_binary() {
    local url=$1
    local checksum=$2
    local install_dir=$3
    local binary_name=$4
    
    # test for existing installation
    if command_exists "${binary_name}"; then
        log_info "${binary_name} is already installed"
        return 0
    fi

    install_tarball "${url}" "${checksum}" "${install_dir}"

    #find the binary in the extracted executables in nested directories
    local binary_path=$(find "${install_dir}" -type f -name "${binary_name}" | head -n 1)

    if [ -z "${binary_path}" ]; then
        log_error "Failed to find binary ${binary_name} in extracted files"
        exit 1
    fi

    # link to the binary in /usr/local/bin and make executable
    ln -s "${binary_path}" /usr/local/bin/${binary_name}
    chmod +x /usr/local/bin/${binary_name}

    # test the binary is now available
    if command_exists "${binary_name}"; then
        log_success "${binary_name} installed successfully"
    else
        log_error "Failed to install ${binary_name}"
        exit 1
    fi
}

# Install .NET SDK
install_dotnet_sdk() {

    if command_exists dotnet; then
        log_info ".NET SDK is already installed"
        return 0
    fi

    log_info "Installing .NET SDK..."

    install_tarball "${DOTNET_URL}" "${DOTNET_CHECKSUM}" "${DOTNET_ROOT}"
}

# Install GitVersion global tool
install_gitversion() {
    if command_exists dotnet && dotnet tool list -g | grep -q gitversion.tool; then
        log_info "GitVersion tool is already installed"
        return 0
    fi

    log_info "Installing GitVersion tool ${GITVERSION_VERSION}..."
    dotnet tool install --global gitversion.tool --version "${GITVERSION_VERSION}"
    
    log_success "GitVersion tool installed"
}

install_cmake() {
    if command_exists cmake; then
        log_info "CMake is already installed"
        return 0
    fi

    local cmake_install_dir="/usr/local/lib/cmake"

    install_tarball "${CMAKE_URL}" "${CMAKE_CHECKSUM}" "${cmake_install_dir}"

    # find the cmake binary location
    local cmake_bin_dir=$(find "${cmake_install_dir}" -type d -name "bin" | head -n 1)
    if [ -z "${cmake_bin_dir}" ]; then
        log_error "Failed to find cmake bin directory after installation"
        exit 1
    fi

    # link all cmake binaries to /usr/local/bin
    ln -s "${cmake_bin_dir}"/* /usr/local/bin
}

# Main installation entry point
install_deps() {
    local os_type=$(detect_os)

    log_info "Detected OS: ${os_type}"

    case "${os_type}" in
        debian)
            log_info "Setting up Debian/Ubuntu environment..."
            
            # Install additional dependencies passed as arguments
            apt-get update -qq
            apt-get install -y "$@" build-essential curl ca-certificates
            ;;
        redhat)
            log_info "Setting up RedHat/Fedora/Alma environment..."
    
            # Collect additional dependencies passed as arguments
            dnf group install -y c-development
            dnf install -y "$@" curl ca-certificates
            ;;
        *)
            log_error "Unsupported OS type: ${os_type}"
            exit 1
            ;;
    esac

    log_info "Base dependencies installed"

    install_cmake

     # Install .NET SDK if not present
    install_dotnet_sdk
    
    # Install GitVersion
    install_gitversion

    # install tarballs
    install_binary "${GOTASK_TAR_URL}" "${GOTASK_CHECKSUM}" "/usr/local/lib/task" "task"
    install_binary "${VNBUILD_URL}" "${VNBUILD_CHECKSUM}" "/usr/local/lib/vnbuild" "vnbuild"

}

# Test if a command is available and print version
test_command() {
    local cmd=$1
    local version_flag=${2:---version}
    
    if command_exists "${cmd}"; then
        local version_output=$(${cmd} ${version_flag} 2>&1 | head -n1 || echo "version unavailable")
        log_success "${cmd} is installed: ${version_output}"
        return 0
    else
        log_error "${cmd} is NOT installed!"
        return 1
    fi
}

# Test all required installations
test_installation() {
    log_info "Verifying installations..."
    
    local failed=0
    
    # Test required commands
    test_command "task" "--version" || ((failed++))
    test_command "vnbuild" "--version" || ((failed++))
    test_command "cmake" "--version" || ((failed++))
    test_command "cpack" "--version" || ((failed++))
    test_command "ctest" "--version" || ((failed++))
    test_command "gcc" "--version" || ((failed++))
    test_command "dotnet" "--version" || ((failed++))
    
    # Test GitVersion tool
    if command_exists dotnet; then
        if dotnet tool list -g | grep -q gitversion.tool; then
            log_success "GitVersion tool is installed"
        else
            log_error "GitVersion tool is NOT installed!"
            ((failed++))
        fi
    fi
    
    if [ ${failed} -eq 0 ]; then
        log_success "All verifications passed!"
        return 0
    else
        log_error "${failed} verification(s) failed!"
        return 1
    fi
}

# ============================================================================
# MAIN SCRIPT EXECUTION
# ============================================================================

# Show usage information
show_usage() {
    cat << EOF
Usage: $0 <command> [options]

Commands:
    install [deps...]    Install all build dependencies (optionally specify additional packages)
    test                 Verify all required tools are installed
    help                 Show this help message

Examples:
    $0 install                    # Install all dependencies
    $0 install libssl-dev         # Install dependencies plus libssl-dev
    $0 test                       # Verify installation

Environment:
    Supports Debian/Ubuntu (apt) and RedHat/Fedora/Alma (dnf) systems.
    Can be run in CI or standalone. Detects and installs .NET SDK if needed.

EOF
}

# Main entry point
main() {
    case "$1" in
        install)
            shift
            install_deps "$@"
            ;;
        test)
            test_installation
            ;;
        help|--help|-h)
            show_usage
            ;;
        *)
            log_error "Unknown command: $1"
            echo ""
            show_usage
            exit 1
            ;;
    esac
}

# Run main function
main "$@"