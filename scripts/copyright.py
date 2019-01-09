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

import os
import re
import sys
import getopt
import difflib
import unittest
import shutil

###################
# global settings #
###################


class Settings(object):

    # working dir; files inside of it will be processed (recursively)
    workingDir = ".."

    # file including copyright notice text;
    # all files with selected extensions must begin with it
    copyrightFile = "copyright_notice.txt"

    # file including whitelisted extensions
    # if empty, all extensions are whitelisted
    whitelistFile = "extension_whitelist.txt"

    # file including blacklisted extensions
    # if empty, none extensions are blacklisted
    blacklistFile = "extension_blacklist.txt"

    # global; true - just check files, false - modify them if needed
    dryrun = True

    # global; true - don't print anything to stdout, false - be verbose
    silent = False

    # create backup files
    backup = False


settings = Settings()

####################
# helper functions #
####################


def printf(s):
    if not settings.silent:
        print(s)


def printBanner(banner):
    length = len(banner) + 2
    printf("\n+" + (length * "=") + "+\n| " +
           banner +
           " |\n+" + (length * "=") + "+\n")


def readList(path):
    with open(path, 'r') as f:
        lines = [l for l in (line.strip() for line in f) if l]
        return lines


def readCopyright(path):
    with open(path, 'r') as f:
        content = f.read()
        return content.strip()


def containsCopyright(content, regex):
    return regex.match(content) is not None


def prepareRegex(copyright):
    return re.compile(r'\s*' + re.sub(r'\r', '\r?\n?',
                      re.escape(copyright)),
                      re.MULTILINE)


def isWhitelisted(path, extensionList):
    return isListed(path, extensionList) or not extensionList


def isBlacklisted(path, extensionList):
    return isListed(path, extensionList)


def isListed(path, extensionList):
    return any(path.endswith(extension) for extension in extensionList)


def displayHelp(scriptName):
    settings.silent = False
    printf("Usage: python %s [OPTION]... [FILE]..." % scriptName)
    printf("Check whitelisted and not blacklisted files for copyright notice.")
    printf("")
    printf("Default mode is settings.dryrun; process returns number of files t"
           "hat needed modifying.")
    printf("List delimeter is new line. Filepath arguments override working di"
           "rectory settings. Default paths:")
    printf("  ..                        working directory")
    printf("  copyright_notice.txt      copyright notice")
    printf("  extension_whitelist.txt   list of whitelisted extensions (if emp"
           "ty, all extensions are whitelisted)")
    printf("  extension_blacklist.txt   list of blacklisted extensions (if emp"
           "ty, none extensions are blacklisted)")
    printf("")
    printf("  -h, --help        display help")
    printf("  -s, --settings.silent      run in settings.silent mode")
    printf("  -d, --dir         working directory")
    printf("  -c, --copyright   copyright file path")
    printf("  -w, --whitelist   whitelist file path")
    printf("  -b, --blacklist   blacklist file path")
    printf("  -t, --test        perform code tests")
    printf("  --backup          create backup files")
    printf("  --wetrun          automatically modify applicable files, prepend"
           "ing/fixing them with copyright notice")
    printf("")
    printf("Examples:")
    printf("  python %s                                     Process default di"
           "rectory (settings.dryrun)." % scriptName)
    printf("  python %s file.cs                             Process file.cs (s"
           "ettings.dryrun)." % scriptName)
    printf("  python %s --wetrun -c copyright.txt file.cs   Process file.cs an"
           "d modify it if it doesn't being with contents of copyright.txt fil"
           "e."
           % scriptName)
    printf("  python %s -s                                  Run settings.silen"
           "tly with default settings." % scriptName)

###############################
# file modification functions #
###############################


# prepends left-stripped file content with copyright and new line
def prependWithCopyright(path, copyright):
    if settings.dryrun:
        return
    with open(path, 'r') as f:
        content = f.read()
    with open(path, 'w') as f:
        f.write(copyright + '\n\n' + content.lstrip())


# generates string of content with copyright prefix it contained
# being rewritten to match proper one
def fixContent(prefix, content):
    # get matching blocks
    blocks = difflib.SequenceMatcher(None, prefix, content)\
            .get_matching_blocks()
    # add prefix to suffix
    return (prefix + content[blocks[-2].b + blocks[-2].size:]
            if blocks else content)


# fixes content copyright prefix so it matches proper one
def fixCopyright(path, copyright):
    if settings.dryrun:
        return
    # read original file contents
    with open(path, 'r') as f:
        content = f.read()
    # generate fixed content
    content = fixContent(copyright, content)
    # overwrite file with generated content
    with open(path, 'w') as f:
        f.write(content)


def createBackupFile(path, extension):
    if settings.dryrun or not settings.backup:
        return
    shutil.copyfile(path, path + "." + extension)

#############################
# file processing functions #
#############################


def getFilelist(rootDir):
    paths = []
    for dirName, subdirList, fileList in os.walk(rootDir):
        if "thirdparty" in dirName or ".git" in dirName:
            continue
        for fname in fileList:
                paths.append(dirName + '/' + fname)
    return paths


def processFile(path, copyright, regex):
    with open(path) as open_file:
        content = open_file.read()
        if not containsCopyright(content, regex):
            diffRatio = difflib.SequenceMatcher(None,
                                                copyright,
                                                content[: len(copyright)])\
                                                        .ratio()
            # 0.6 is regarded as threshold for diff similarity
            closeEnough = diffRatio >= 0.6
            printf("%s | %f | %s" % ("~REPLACE" if closeEnough else "+PREPEND",
                                     diffRatio,
                                     path))
            createBackupFile(path, "bak")
            if closeEnough:
                fixCopyright(path, copyright)
                return "fix"
            else:
                prependWithCopyright(path, copyright)
                return "prep"
        else:
            return "ok"


def processFiles(paths, copyright, whitelist, blacklist):
    regex = prepareRegex(copyright)
    printf(" ACTION  | MATCHING | PATH")
    printf("---------+----------+-----------------------------------")
    prepCnt = 0
    fixCnt = 0
    okCnt = 0
    notCnt = 0
    for path in paths:
        if isWhitelisted(path, whitelist)\
           and not isBlacklisted(path, blacklist):
            try:
                case = processFile(path, copyright, regex)
                if case == "fix":
                    fixCnt += 1
                elif case == "prep":
                    prepCnt += 1
                elif case == "ok":
                    okCnt += 1
            except(Exception):
                notCnt += 1
        else:
            notCnt += 1
    printf("\n%d files checked" % (fixCnt + prepCnt + okCnt + notCnt))
    printf("--------------------")
    printf("%d files not applicable" % notCnt)
    printf("%d files were correct" % okCnt)
    printf("%d files needed prepending" % prepCnt)
    printf("%d files needed fixing" % fixCnt)
    if settings.dryrun:
        printf("\nNo changes have been made (settings.dryrun;"
               " run with --wetrun argument to alter files).")
    else:
        printf("\n%d files have been changed." % (fixCnt + prepCnt))
        if settings.backup:
            printf("\nThey have been backed up.")
    return fixCnt + prepCnt

##############
# unit tests #
##############


class TestMethods(unittest.TestCase):

    tempdir = "unittesttempdir"

    def setUp(self):
        os.mkdir(self.tempdir)
        os.chdir(self.tempdir)
        global settings
        settings.dryrun = False
        settings.silent = True

    def tearDown(self):
        os.chdir("..")
        shutil.rmtree(self.tempdir)
        global settings
        settings.dryrun = True
        settings.silent = False

    def genericTest(self, prefix, content, expected, changes):
        tmpfile = "tmp.cs"
        with open(tmpfile, "w") as f:
            f.write(content)
        out = processFiles([tmpfile], prefix, [], [])
        with open(tmpfile, "r") as f:
            result = f.read()
        self.assertEqual(result, expected)
        self.assertEqual(out, changes)

    def test_correct1(self):
        self.genericTest(prefix="12345",
                         content="12345\n\nasddfg",
                         expected="12345\n\nasddfg",
                         changes=0)

    def test_correct2(self):
        self.genericTest(prefix="12345",
                         content="12345\n\n12345",
                         expected="12345\n\n12345",
                         changes=0)

    def test_prepend1(self):
        self.genericTest(prefix="12345",
                         content="asddfg",
                         expected="12345\n\nasddfg",
                         changes=1)

    def test_prepend2(self):
        self.genericTest(prefix="12345",
                         content="\nasddfg",
                         expected="12345\n\nasddfg",
                         changes=1)

    def test_replace1(self):
        self.genericTest(prefix="12345",
                         content="1245\n\nasddfg",
                         expected="12345\n\nasddfg",
                         changes=1)

    def test_replace2(self):
        self.genericTest(prefix="12345",
                         content="ad1245\n\nasddfg",
                         expected="12345\n\nasddfg",
                         changes=1)


def performTests():
    # rewrite sys arguments (unittest uses them)
    sys.argv = [sys.argv[0], "--verbose"]
    # run tests
    unittest.main()


#################
# main function #
#################


if __name__ == "__main__":
    # getopts
    unixOpts = "hsd:c:w:b:t"
    gnuOpts = ["help",
               "settings.silent",
               "dir=",
               "copyright=",
               "whitelist=",
               "blacklist=",
               "wetrun",
               "backup",
               "test"]
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
        elif arg in ("-t", "--test"):
            performTests()
            sys.exit(0)
        elif arg in ("-s", "--settings.silent"):
            settings.silent = True
        elif arg in ("-d", "--dir"):
            settings.workingDir = val
        elif arg in ("-c", "--copyright"):
            settings.copyrightFile = val
        elif arg in ("-w", "--whitelist"):
            settings.whitelistFile = val
        elif arg in ("-b", "--blacklist"):
            settings.blacklistFile = val
        elif arg in ("--backup"):
            settings.backup = True
        elif arg in ("--wetrun"):
            settings.dryrun = False

    # read required data
    printBanner("SETTING UP VARIABLES")
    printf("[!] settings.dryrun='%s' (%s)\n"
           % (settings.dryrun,
              "files won't be altered;"
              " run with --wetrun argument to alter files"
               if settings.dryrun else "files will be altered!"))
    printf("[!] settings.silent='False'\n")
    copyright = readCopyright(settings.copyrightFile)
    printf("[!] copyright='%s'\n" % copyright)
    whitelist = readList(settings.whitelistFile)
    printf("[!] whitelist=%s\n" % whitelist)
    blacklist = readList(settings.blacklistFile)
    printf("[!] blacklist=%s\n" % blacklist)
    printf("[!] settings.workingDir='%s'\n" % settings.workingDir)
    printf("[!] filelist=%s" % vals)

    if not vals:
        printBanner("PROCESSING FILES")
        paths = getFilelist(settings.workingDir)
    else:
        printBanner("PROCESSING DIRECTORY '%s'" % settings.workingDir)
        paths = vals

    numberOfFilesWithoutProperCopyrightNotice = processFiles(paths,
                                                             copyright,
                                                             whitelist,
                                                             blacklist)
    printBanner("PROCESSING DONE")
    sys.exit(numberOfFilesWithoutProperCopyrightNotice)
