# LightSync ( ⚠️ WIP ⚠️ )

A set of scripts for syncing an object in VRChat with an emphasis on network efficiency, and overengineering every little thing so that it works in as many situations as possible.

Prefab is a work in progress so things might change drastically between updates and possibly break things. If you want a more stable alternative, check out [Smart Object Sync](https://github.com/MMMaellon/SmartObjectSync).

# DOWNLOAD

Download and Install thru VCC: <https://mmmaellon.github.io/LightSync/>

Download all my VCC packages here: <https://mmmaellon.github.io/MMMaellonVCCListing/>

# USAGE

To use it, just add the `LightSync` component onto an object. Out of the box it'll handle the most common use cases of syncing physics and pickup events, but you can add additional scripts to handle things like attaching an object to a player.

# Ask questions on my [Discord Server](https://discord.gg/S5sDC4PnFp)

# What is the difference between this and SmartObjectSync

[Smart Object Sync](https://github.com/MMMaellon/SmartObjectSync) was my first attempt at making my own custom syncing scripts. It works well, but I learned a lot since and realized I could do better. So I started over with LightSync.

Some highlights of how it's better:
1. It uses almost half as much network bandwidth.
2. It lags less. (Tested in sample scene with 1,200 synced and colliding rigidbodies)
3. It handles unstable network latency better.
4. It has better compatibility with other systems like vehicles and AI which SmartObjectSync struggles with.
5. It's new and shiny.

I use SmartObjectSync a lot and it's a dependency of many of my other prefabs. However, since LightSync is better in basically every way, I will most likely be migrating some prefabs to LightSync as well as any future prefabs.
