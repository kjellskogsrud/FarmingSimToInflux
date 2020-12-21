# FarmingSim to InfluxDB
Reads savegame data from FarmingSim 2019 and store in InfluxDB.

## Disclaimer
It is probably full of problems and not intended to be used for any thing other than fun. 
It took all of maybe 2 hours to but together, and is full of terrible brute force ways of doing thing.

## How to use?
Just run the program. The first time it will exit almost immediately. This is because it makes a sample config.xml file
if one is not found. 

Edit the config.xml file with your details. These are pretty self-explaining.

Then just run the program again, and data will role into influx every 5 minutes.
NOTE! you have to make the influx database manually first. i.e: "CREATE DATABASE FarmingSim"

## What this is not!
Due to the way FS19 saves files there are some limitations on when you get data.
FS has an auto save interval in settings, however the game only ever saves when you are in the menus. Even with an auto save interval of 5 min. It can take longer than this before data is saved, so the Influx data is not "live". Just remember to open the menu every 5 ish minutes and it will save. Most players probably do this to look at field info and overlays for growth state, fertilizer etc. 