import os
import re

def readCopyright(path):
    with open(path, 'r') as f:
        data = f.read()
        print(data)
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

def processDir(rootDir, copyright):
    for dirName, subdirList, fileList in os.walk(rootDir):
        for fname in fileList:
            if fname.endswith(".cs") and fname.count('.') == 1:
                processFile(dirName + '/' + fname, copyright)

if __name__ == "__main__":
    processDir('..', readCopyright("copyright.txt"))
