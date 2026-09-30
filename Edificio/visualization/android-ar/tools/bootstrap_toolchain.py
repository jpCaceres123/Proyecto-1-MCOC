from pathlib import Path
import urllib.request, zipfile, concurrent.futures
root=Path.home()/'.codex'/'android-ar-tools'
root.mkdir(parents=True,exist_ok=True)
items=[('jdk.zip','https://aka.ms/download-jdk/microsoft-jdk-17-windows-x64.zip','jdk'),('gradle.zip','https://services.gradle.org/distributions/gradle-8.10.2-bin.zip','gradle'),('sdk.zip','https://dl.google.com/android/repository/commandlinetools-win-11076708_latest.zip','sdk')]
def download(item):
 name,url,folder=item
 destination=root/folder
 if destination.exists(): print('EXISTS',destination,flush=True);return
 archive=root/name
 print('DOWNLOADING',name,flush=True)
 urllib.request.urlretrieve(url,archive)
 print('EXTRACTING',name,archive.stat().st_size,flush=True)
 destination.mkdir(exist_ok=True)
 with zipfile.ZipFile(archive) as z:z.extractall(destination)
 print('READY',destination,flush=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:list(pool.map(download,items))
