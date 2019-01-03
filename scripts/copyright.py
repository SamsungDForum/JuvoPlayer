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

import os, re, difflib

def readList(path):
    with open(path, 'r') as f:
        lines = [l for l in (line.strip() for line in f) if l]
        return lines

def readCopyright(path):
    with open(path, 'r') as f:
        data = f.read()
        return data.strip()

def prependWithCopyright(path, copyright):
    return
    with open(path, 'r+') as f:
        data = f.read()
        f.seek(0)
        f.write(copyright + "\n" + data)

def fixCopyright(path, copyright, sequenceMatcher):
    print sequenceMatcher.get_matching_blocks()
    return

def containsCopyright(data, regex):
    return regex.match(data) != None

def processFile(path, copyright, regex):
    with open(path) as open_file:
        data = open_file.read()
        if not containsCopyright(data, regex):
            sequenceMatcher = difflib.SequenceMatcher(isjunk=None, a=copyright, b=data[:len(copyright)])
            ratio = sequenceMatcher.ratio()
            closeEnough = ratio >= 0.6
            print("[%s] %f - %s" % ("-+" if closeEnough else "++", ratio, path))
            if closeEnough:
                fixCopyright(path, copyright, sequenceMatcher)
            else:
                prependWithCopyright(path, copyright)

def prepareRegex(copyright):
    return re.compile("\s*" + re.sub(r'\r', '\r?\n?', re.escape(copyright)), re.MULTILINE)

def isWhitelisted(path, extensionList):
    return isListed(path, extensionList) or not extensionList

def isBlacklisted(path, extensionList):
    return isListed(path, extensionList)

def isListed(path, extensionList):
    return any(path.endswith(extension) for extension in extensionList)

def processDir(rootDir, copyright, whitelist, blacklist):
    regex = prepareRegex(copyright)
    for dirName, subdirList, fileList in os.walk(rootDir):
        if "thirdparty" in dirName or ".git" in dirName:
            continue
        for fname in fileList:
            path = dirName + '/' + fname
            if isWhitelisted(path, whitelist) and not isBlacklisted(path, blacklist):
                processFile(path, copyright, regex)

if __name__ == "__main__":
    directory = ".."
    print "[!] directory=%s\n" % directory
    copyright = readCopyright("copyright_notice.txt")
    print "[!] copyright=%s\n" % copyright
    whitelist = readList("extension_whitelist.txt")
    print "[!] whitelist=%s\n" % whitelist
    blacklist = readList("extension_blacklist.txt")
    print "[!] blacklist=%s\n" % blacklist
    processDir(directory, copyright, whitelist, blacklist)
