# Getting started
(WIP)
* Create an instance of the `GameData` class to access data
* Call `GameData.Indexer.LoadFromDataPath(string dataPath)` to initialize the index (this may take a long time)
* Files can be loaded either in full, or just with their headers
* Files can be loaded eagerly, or with `GameData.GetFileHandle<T>`, which loads files in the background