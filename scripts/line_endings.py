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

import os, re

def readBlacklist(path):
    with open(path, 'r') as f:
        lines = [l for l in (line.strip() for line in f) if l]
        return lines

def containsCarriageReturn(data, regex):
    return regex.match(data) != None

def processFile(path, copyright, regex):
    with open(path) as open_file:
        data = open_file.read()
        if containsCarriageReturn(data, regex):
            print("[+] %s" % path)
        else:
            print("[ ] %s" % path)

def prepareRegex(copyright):
    return re.compile(r'\r', re.MULTILINE)

def isApplicable(path, blacklist):
    return path.endswith(".cs") and all(not path.endswith(extension) for extension in blacklist) and "thirdparty/" not in path

def processDir(rootDir, blacklist):
    regex = prepareRegex(copyright)
    for dirName, subdirList, fileList in os.walk(rootDir):
        for fname in fileList:
            if isApplicable(dirName + '/' + fname, blacklist):
                processFile(dirName + '/' + fname, copyright, regex)

if __name__ == "__main__":
    processDir('..', readBlacklist("extension_blacklist.txt"))
