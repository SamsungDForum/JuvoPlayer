# https://github.com/SamsungDForum/JuvoPlayer
# Copyright 2018, Samsung Electronics Co., Ltd
# Licensed under the MIT license
#
# THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
# AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
# IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
# ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
# DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
# (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
# LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
# ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
# (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
# SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

import os, re, sys, getopt, difflib

###########
# globals #
###########

WORKING_DIR = ".."                           # working dir; files inside of it will be processed (recursively)
COPYRIGHT_FILE = "copyright_notice.txt"      # file including copyright notice text; all files with selected extensions must begin with it
WHITELIST_FILE = "extension_whitelist.txt"   # file including whitelisted extensions (if empty, all extensions are whitelisted)
BLACKLIST_FILE = "extension_blacklist.txt"   # file including blacklisted extensions (if empty, none extensions are blacklisted)
DRYRUN = True                                # global; true - just check files, false - modify them if needed
SILENT = False                               # global; true - don't print anything to stdout, false - be verbose

####################
# helper functions #
####################

def printf(s):
    if not SILENT:
        print(s)

def printBanner(banner):
    l = len(banner) + 2
    printf("\n+" + (l * "=") + "+\n| " + banner + " |\n+" + (l * "=") + "+\n")

def readList(path):
    with open(path, 'r') as f:
        lines = [l for l in (line.strip() for line in f) if l]
        return lines

def readCopyright(path):
    with open(path, 'r') as f:
        data = f.read()
        return data.strip()

def containsCopyright(data, regex):
    return regex.match(data) != None

def prepareRegex(copyright):
    return re.compile("\s*" + re.sub(r'\r', '\r?\n?', re.escape(copyright)), re.MULTILINE)

def isWhitelisted(path, extensionList):
    return isListed(path, extensionList) or not extensionList

def isBlacklisted(path, extensionList):
    return isListed(path, extensionList)

def isListed(path, extensionList):
    return any(path.endswith(extension) for extension in extensionList)

def displayHelp(scriptName):
    SILENT = False
    printf("Usage: python %s [OPTION]... [FILE]..." % scriptName)
    printf("Check whitelisted and not blacklisted files for copyright notice.")
    printf("")
    printf("Default mode is dryrun; process returns number of files that needed modifying.")
    printf("List delimeter is new line. Default paths:")
    printf("  ..                        working directory")
    printf("  copyright_notice.txt      copyright notice")
    printf("  extension_whitelist.txt   list of whitelisted extensions (if empty, all extensions are whitelisted)")
    printf("  extension_blacklist.txt   list of blacklisted extensions (if empty, none extensions are blacklisted)")
    printf("")
    printf("  -h, --help        display help")
    printf("  -s, --silent      run in silent mode")
    printf("  -d, --dir         working directory")
    printf("  -c, --copyright   copyright file path")
    printf("  -w, --whitelist   whitelist file path")
    printf("  -b, --blacklist   blacklist file path")
    printf("  --wetrun          automatically modify applicable files, prepending/fixing them with copyright notice")
    printf("")
    printf("Examples:")
    printf("  python %s                                     Process default directory (dryrun)." % scriptName)
    printf("  python %s file.cs                             Process file.cs (dryrun)." % scriptName)
    printf("  python %s --wetrun -c copyright.txt file.cs   Process file.cs and modify it if it doesn't being with contents of copyright.txt file." % scriptName)
    printf("  python %s -s                                  Run silently with default settings." % scriptName)

###############################
# file modification functions #
###############################

def prependWithCopyright(path, copyright):
    if DRYRUN:
        return
    with open(path, 'r+') as f:
        data = f.read()
        f.seek(0)
        f.write(copyright + "\n" + data)

def fixContent(prefix, content):
    blocks = difflib.SequenceMatcher(None, prefix, content).get_matching_blocks() # get matching blocks
    return prefix + content[blocks[-2].b + 1:] if blocks else content     # add prefix to suffix

def fixCopyright(path, copyright):
    if DRYRUN:
        return
    with open(path, 'r') as f:               # read original file contents
        content = f.read()
    content = fixContent(copyright, content) # generate fixed content
    with open(path, 'w') as f:               # overwrite file with generated content
        f.write(content)

#############################
# file processing functions #
#############################

def processFile(path, copyright, regex):
    with open(path) as open_file:
        data = open_file.read()
        if not containsCopyright(data, regex):
            diffRatio = difflib.SequenceMatcher(None, copyright, data[: len(copyright)]).ratio()
            closeEnough = diffRatio >= 0.6
            printf("%s | %f | %s" % ("~REPLACE" if closeEnough else "+PREPEND", diffRatio, path))
            if closeEnough:
                fixCopyright(path, copyright)
                return "fix"
            else:
                prependWithCopyright(path, copyright)
                return "prep"
        else:
            return "ok"

def processDir(rootDir, copyright, whitelist, blacklist):
    regex = prepareRegex(copyright)
    printf(" ACTION  | MATCHING | PATH")
    printf("---------+----------+-----------------------------------")
    prepCnt = 0
    fixCnt = 0
    okCnt = 0
    notCnt = 0
    for dirName, subdirList, fileList in os.walk(rootDir):
        if "thirdparty" in dirName or ".git" in dirName:
            continue
        for fname in fileList:
            path = dirName + '/' + fname
            if isWhitelisted(path, whitelist) and not isBlacklisted(path, blacklist):
                case = processFile(path, copyright, regex)
                if case == "fix":
                    fixCnt += 1
                elif case == "prep":
                    prepCnt += 1
                elif case == "ok":
                    okCnt += 1
            else:
                notCnt += 1
    printf("\n%d files checked" % (fixCnt + prepCnt + okCnt + notCnt))
    printf("--------------------")
    printf("%d files not applicable" % notCnt)
    printf("%d files were correct" % okCnt)
    printf("%d files needed prepending" % prepCnt)
    printf("%d files needed fixing" % fixCnt)
    if DRYRUN:
        printf("\nNo changes have been made (dryrun; run with --wetrun argument to alter files).")
    else:
        printf("\n%d files have been changed." % (fixCnt + prepCnt))
    return fixCnt + prepCnt

#################
# main function #
#################

if __name__ == "__main__":
    # getopts
    unixOpts = "hsd:c:w:b:"
    gnuOpts = ["help", "silent", "dir=", "copyright=", "whitelist=", "blacklist=", "wetrun"]
    argList = (sys.argv)[1:]
    try:
        args, vals = getopt.getopt(argList, unixOpts, gnuOpts)
    except getopt.error as err:
        print(str(err))
        sys.exit(-1)
    for arg, val in args:
        if arg in ("-h", "--help"):
            displayHelp(sys.argv[0])
            sys.exit(-2)
        elif arg in("-s", "--silent"):
            SILENT = True
        elif arg in("-d", "--dir"):
            WORKING_DIR = val
        elif arg in("-c", "--copyright"):
            COPYRIGHT_FILE = val
        elif arg in("-w", "--whitelist"):
            WHITELIST_FILE = val
        elif arg in("-b", "--blacklist"):
            BLACKLIST_FILE = val
        elif arg in("--wetrun"):
            DRYRUN = False

    # read required data
    printBanner("SETTING UP VARIABLES")
    printf("[!] DRYRUN='%s' (%s)\n" % (DRYRUN, "files won't be altered; run with --wetrun argument to alter files" if DRYRUN else "files will be altered!"))
    printf("[!] SILENT='False'\n")
    copyright = readCopyright(COPYRIGHT_FILE)
    printf("[!] copyright='%s'\n" % copyright)
    whitelist = readList(WHITELIST_FILE)
    printf("[!] whitelist=%s\n" % whitelist)
    blacklist = readList(BLACKLIST_FILE)
    printf("[!] blacklist=%s\n" % blacklist)
    printf("[!] WORKING_DIR='%s'" % WORKING_DIR)

    # process files
    printBanner("PROCESSING DIRECTORY '%s'" % WORKING_DIR)
    numberOfFilesWithoutProperCopyrightNotice = processDir(WORKING_DIR, copyright, whitelist, blacklist)
    printBanner("PROCESSING DONE")
    sys.exit(numberOfFilesWithoutProperCopyrightNotice)

