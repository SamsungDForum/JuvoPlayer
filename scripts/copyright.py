import os
import re

def readBlacklist(path):
    with open(path, 'r') as f:
        data = f.readlines()
        return data

def readCopyright(path):
    with open(path, 'r') as f:
        data = f.read()
        return data

def writeCopyright(path, copyright):
    with open(path, 'r+') as f:
        data = f.read()
        f.seek(0)
        f.write(copyright + "\n" + data)

def containsCopyright(data, copyright):
    return None != re.match("\s*" + re.escape(copyright), data)

def processFile(path, copyright):
    with open(path) as open_file:
        data = open_file.read()
        if not containsCopyright(data, copyright):
            print("[+] %s" % path)
            writeCopyright(path, copyright)
        else:
            print("[ ] %s" % path)

def isApplicable(path, blacklist):
    return fname.endswith(".cs") and all(not fname.endswith(extension) for extension in blacklist) and "thirdparty/" not in path

def processDir(rootDir, copyright, blacklist):
    for dirName, subdirList, fileList in os.walk(rootDir):
        for fname in fileList:
            if isApplicable(dirName + '/' + fname, blacklist):
                processFile(dirName + '/' + fname, copyright)

if __name__ == "__main__":
    processDir('..', readCopyright("copyright_notice.txt"), readBlacklist("extension_blacklist.txt"))
