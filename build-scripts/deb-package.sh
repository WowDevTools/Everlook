#!/bin/bash

# Move to the base folder where the script is located.
cd $(dirname $0)

PROGRAM_ROOT=$(readlink -f "..")
OUTPUT_ROOT="$PROGRAM_ROOT/release"

RED='\033[0;31m'
GREEN='\033[0;32m'
ORANGE='\033[0;33m'
LOG_PREFIX="${GREEN}[Everlook]:"
LOG_PREFIX_ORANGE="${ORANGE}[Everlook]:"
LOG_PREFIX_RED="${RED}[Everlook]:"
LOG_SUFFIX='\033[0m'

PROGRAM_NAME="Everlook"
BINARY_EXTENSION="exe"

getopt --test > /dev/null
if [[ $? -ne 4 ]]; then
    echo "I’m sorry, $(getopt --test) failed in this environment."
    exit 1
fi

SHORTOPTS=k:v
LONGOPTS=key:,verbose

PARSED=$(getopt --options $SHORTOPTS --longoptions $LONGOPTS --name "$0" -- "$@")

if [[ $? -ne 0 ]]; then
    # e.g. $? == 1
    #  then getopt has complained about wrong arguments to stdout
    exit 2
fi

# use eval with "$PARSED" to properly handle the quoting
eval set -- "$PARSED"

# now enjoy the options in order and nicely split until we see --
while true; do
    case "$1" in
        -v|--verbose)
            VERBOSE=y
            shift
            ;;
        -y|--always-yes)
            ALWAYSYES=y
            shift
            ;;
        -k|--key)
            SIGNINGKEY="$2"
            shift 2
            ;;
        --)
            shift
            break
            ;;
        *)
            echo "Programming error"
            exit 3
            ;;
    esac
done

# handle non-option arguments
if [ -v $SIGNINGKEY ]; then
    echo "$0: A signing key is required."
    exit 4
fi

echo -e "$LOG_PREFIX Building Release configuration of $PROGRAM_NAME... $LOG_SUFFIX"

BUILDSUCCESS=$(xbuild /p:Configuration="Release" "$PROGRAM_ROOT/$PROGRAM_NAME.sln"  | grep "Build succeeded.")

if [[ ! -z $BUILDSUCCESS ]]; then
	echo "Build succeeded. Copying files and building package."
	# The library builds, so we can proceed
	PROGRAM_ASSEMBLY_VERSION=$(monodis --assembly "$PROGRAM_ROOT/$PROGRAM_NAME/bin/Release/$PROGRAM_NAME.$BINARY_EXTENSION" | grep Version | egrep -o '[0-9]*\.[0-9]*\.[0-9]*\.[0-9]*d*')
	PROGRAM_MAJOR_VERSION=$(echo "$PROGRAM_ASSEMBLY_VERSION" | awk -F \. {'print $1'})
	PROGRAM_MINOR_VERSION=$(echo "$PROGRAM_ASSEMBLY_VERSION" | awk -F \. {'print $2'})

	PROGRAM_VERSIONED_NAME="$PROGRAM_NAME-$PROGRAM_ASSEMBLY_VERSION"
	PROGRAM_TARBALL_NAME="${PROGRAM_NAME,,}"_"$PROGRAM_ASSEMBLY_VERSION"
	PROGRAM_DEBUILD_ROOT="$OUTPUT_ROOT/$PROGRAM_VERSIONED_NAME"
	
	# Update Debian changelog
	cd $PROGRAM_ROOT
	dch -v $PROGRAM_ASSEMBLY_VERSION-1
	cd - > /dev/null

	if [ ! -d "$PROGRAM_DEBUILD_ROOT" ]; then
		# Clean the sources
		rm -rf "$PROGRAM_ROOT/$PROGRAM_NAME/bin"
		rm -rf "$PROGRAM_ROOT/$PROGRAM_NAME/obj"
	
		# Copy the sources to the build directory
		mkdir -p "$PROGRAM_DEBUILD_ROOT"
		cp -r "$PROGRAM_ROOT/debian/" $PROGRAM_DEBUILD_ROOT
		cp -r "$PROGRAM_ROOT/lib/" $PROGRAM_DEBUILD_ROOT
		cp -r "$PROGRAM_ROOT/linux-desktop/" $PROGRAM_DEBUILD_ROOT
		cp -r "$PROGRAM_ROOT/$PROGRAM_NAME/" $PROGRAM_DEBUILD_ROOT
		cp "$PROGRAM_ROOT/"* "$PROGRAM_DEBUILD_ROOT"

		# Pull in the NuGet dependencies
		echo -e "$LOG_PREFIX_ORANGE Pulling in NuGet dependencies... $LOG_SUFFIX"
		nuget restore "$PROGRAM_DEBUILD_ROOT/$PROGRAM_NAME.sln"

		# Create an *.orig.tar.xz archive if one doesn't exist already
		ORIG_TAR="$OUTPUT_ROOT/$PROGRAM_TARBALL_NAME.orig.tar.xz"
		if [ ! -f "$ORIG_TAR" ]; then
			cd "$PROGRAM_DEBUILD_ROOT/"
			tar -cJf "$ORIG_TAR" "."
			cd - > /dev/null
		fi
		
		# Build the debian package
		read -p "Ready to build the debian package. Continue? [y/N] " -n 1 -r
		echo

		if [[ $REPLY =~ ^[Yy]$ ]] || [[ ! -z $ALWAYSYES ]]
		then
			cd "$PROGRAM_DEBUILD_ROOT"
			debuild -S -k$SIGNINGKEY
		fi							
	fi
else
	echo "The build failed. Aborting."
fi
