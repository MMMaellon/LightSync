import os

rootPath = os.path.dirname(os.path.abspath(__file__))
oldLowerCaseName = "lower-case-name"
oldUpperCaseName = "STARDENBURDENHARDENBART"

print("renaming all files in: " + rootPath)

lowerCaseName = " "

while lowerCaseName.count(" ") > 0:
    lowerCaseName = input("lower-case-name: ").lower()
upperCaseName = input("Upper Case Name: ")

confirm = ""
while len(confirm) <= 0:
    confirm = input("confirm? [y/n]: ")

if confirm != "y":
    print("cancelling")
    exit()

dryRun = False


def renameFolders(root, oldString, newString):
    for subdir, dirs, _ in os.walk(root):
        for dir in dirs:
            if dir.count(oldString) > 0:
                path = os.path.join(subdir, dir)
                path = os.path.abspath(path)
                newPath = os.path.join(subdir, dir.replace(oldString, newString))
                newPath = os.path.abspath(newPath)
                if dryRun:
                    print(newPath)
                else:
                    os.rename(path, newPath)  # rename your file


def renameFilenames(root, oldString, newString):
    for subdir, _, files in os.walk(root):
        for filename in files:
            if filename.find(oldString) > 0:
                filePath = os.path.join(subdir, filename)
                newFileName = filename.replace(
                    oldString, newString
                )  # create the new name
                newFilePath = os.path.join(
                    subdir, newFileName
                )  # get the path to your file
                newFilePath = os.path.abspath(newFilePath)
                if dryRun:
                    print(newFileName)
                else:
                    os.rename(filePath, newFilePath)  # rename your file


def renameInFilenames(root, oldString, newString):
    for subdir, _, files in os.walk(root):
        for filename in files:
            filePath = os.path.join(subdir, filename)

            print(filePath)

            # Read in the file
            with open(filePath, "r") as file:
                try:
                    filedata = file.read()
                except:
                    continue
            if filedata.count(oldString) <= 0:
                continue
            filedata = filedata.replace(oldString, newString)
            if dryRun:
                for line in filedata.splitlines():
                    # print(line)
                    pass
            else:
                with open(filePath, "w") as outfile:
                    outfile.write(filedata)


roots = [os.path.join(rootPath, "Packages"), os.path.join(rootPath, ".github")]

for root in roots:
    print("Renaming Folders: ")
    renameFolders(root, oldLowerCaseName, lowerCaseName)
    print("Renaming Files: ")
    renameFilenames(root, oldLowerCaseName, lowerCaseName)
    print("Renaming Inside Files: ")
    renameInFilenames(root, oldLowerCaseName, lowerCaseName)
    renameInFilenames(root, oldUpperCaseName, upperCaseName)
