import os, re

def readBlacklist(path):
    with open(path, 'r') as f:
        lines = [l for l in (line.strip() for line in f) if l]
        return lines

def readCopyright(path):
    with open(path, 'r') as f:
        data = f.read()
        return data.strip()

def writeCopyright(path, copyright):
    with open(path, 'r+') as f:
        data = f.read()
        f.seek(0)
        f.write(copyright + "\n" + data)

def containsCopyright(data, regex):
    return regex.match(data) != None

def processFile(path, copyright, regex):
    with open(path) as open_file:
        data = open_file.read()
        if not containsCopyright(data, regex):
            print("[+] %s" % path)
            writeCopyright(path, copyright)
        else:
            print("[ ] %s" % path)

def prepareRegex(copyright):
    return re.compile("\s*" + re.sub(r'\n', '\r?\n', re.escape(copyright)), re.MULTILINE)

def isApplicable(path, blacklist):
    return path.endswith(".cs") and all(not path.endswith(extension) for extension in blacklist) and "thirdparty/" not in path

def processDir(rootDir, copyright, blacklist):
    regex = prepareRegex(copyright)
    for dirName, subdirList, fileList in os.walk(rootDir):
        for fname in fileList:
            if isApplicable(dirName + '/' + fname, blacklist):
                processFile(dirName + '/' + fname, copyright, regex)

if __name__ == "__main__":
    processDir('..', readCopyright("copyright_notice.txt"), readBlacklist("extension_blacklist.txt"))
