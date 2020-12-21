using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.XPath;
using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using InfluxData.Net.InfluxDb.Models;

namespace FarmingSimToInflux
{
    class Program
    {
        static void Main(string[] args)
        {
            // Read Configuration
            CheckForConfigFile();
            Configuration configuration = ReadConfiguration();

            while (true)
            {
                GetDataFromFilesAndWriteToInflux(configuration);
                Thread.Sleep(60000 * 5); // 5 min
            }
        }

        static void GetDataFromFilesAndWriteToInflux(Configuration config)
        {
            string farmsFile = config.SaveGameFolder+"farms.xml";
            string itemsFile = config.SaveGameFolder + "items.xml";
            string environmentFile = config.SaveGameFolder + "environment.xml";
            string seasonsFile = config.SaveGameFolder + "seasons.xml";
            string careerSavegameFile = config.SaveGameFolder + "careerSavegame.xml";

            // Is there a seasons.xml file?
            bool hasSeasons = File.Exists(seasonsFile);

            XPathDocument farmsDoc = new XPathDocument(farmsFile);
            XPathNavigator farmsNav = farmsDoc.CreateNavigator();

            XPathDocument itemsDoc = new XPathDocument(itemsFile);
            XPathNavigator itemsNav = itemsDoc.CreateNavigator();

            XPathDocument environmentDoc = new XPathDocument(environmentFile);
            XPathNavigator environmentNav = environmentDoc.CreateNavigator();

            XPathDocument careerSavegameDoc = new XPathDocument(careerSavegameFile);
            XPathNavigator careerSavegameNav = careerSavegameDoc.CreateNavigator();

            XPathDocument seasonsDoc = new XPathDocument(seasonsFile);
            XPathNavigator seasonsNav = seasonsDoc.CreateNavigator();

            // Get the savegameName
            string savegameName = careerSavegameNav.SelectSingleNode("/careerSavegame/settings/savegameName").Value;

            // Get the mapTitle
            string mapTitle = careerSavegameNav.SelectSingleNode("/careerSavegame/settings/mapTitle").Value;

            // Get the environment.xml day
            int.TryParse(environmentNav.SelectSingleNode("/environment/currentDay").ToString(), out int currentDay);

            // Get the days per season
            int.TryParse(seasonsNav.SelectSingleNode("/seasons/environment/daysPerSeason").ToString(), out int daysPerSeason);

            // Get the currentDayOffset
            int.TryParse(seasonsNav.SelectSingleNode("/seasons/environment/currentDayOffset").ToString(), out int currentDayOffset);

            // Calculate the Seasons day
            int daysInYear = daysPerSeason * 4;
            int seasonsDay = currentDay + currentDayOffset;
            int dayInYear = seasonsDay % daysInYear;
            

            Console.WriteLine("{0} Read farm sim data",DateTime.Now);

            Dictionary<string, float> allSilos = new Dictionary<string, float>();
            Dictionary<string, Bunker> allBunkerSiloes = new Dictionary<string, Bunker>();
            Dictionary<string, AnimalHusbandry> allAnimalHusbandry = new Dictionary<string, AnimalHusbandry>();
            // Get all items
            XPathNodeIterator items = itemsNav.Select("/items/item");
            foreach (XPathNavigator item in items)
            {
                // Check for silos that I own. 
                if (item.GetAttribute("className", "") == "SiloPlaceable" && item.GetAttribute("farmId", "") == config.FarmID.ToString())
                {
                    // Get all the storage nodes
                    XPathNodeIterator storageNodes = item.Select("storage/node");
                    foreach(XPathNavigator storageNode in storageNodes)
                    {
                        if(!allSilos.ContainsKey(storageNode.GetAttribute("fillType","")))
                        {
                            float.TryParse(storageNode.GetAttribute("fillLevel", "").Replace('.',','), out float fillLevel);
                            allSilos.Add(storageNode.GetAttribute("fillType", ""), fillLevel);
                        }
                        else
                        {
                            float.TryParse(storageNode.GetAttribute("fillLevel", ""), out float fillLevel);
                            allSilos[storageNode.GetAttribute("fillType", "")] += fillLevel;
                        }
                    }
                }

                // Bunker siloes, regardless of ownership. Some of the built in ones for a map are not farm owned but you can still use them
                if (item.GetAttribute("className", "") == "BunkerSiloPlaceable")
                {
                    XPathNodeIterator bunkerSiloes = item.Select("bunkerSilo");
                    foreach (XPathNavigator bs in bunkerSiloes)
                    {
                        // We use position to make an ID, since ID changes with items in game. So there is no ID persistance. 
                        // It is however extremly unlikely that any bunker will have the exact same X coord as something else. 
                        string BunkerSiloPosition = item.GetAttribute("position", "");
                        string firstCoord = BunkerSiloPosition.Split(' ')[0].Split('.')[0];
                        int.TryParse(firstCoord, out int BunkerSiloId);
                        BunkerSiloId = Math.Abs(BunkerSiloId);

                        Bunker thisBunker = new Bunker();
                        int.TryParse(bs.GetAttribute("state", ""),out thisBunker.state);
                        float.TryParse(bs.GetAttribute("fillLevel", "").Replace('.', ','), out thisBunker.fillLevel);
                        float.TryParse(bs.GetAttribute("compactedFillLevel", "").Replace('.', ','), out thisBunker.compactedFillLevel);
                        float.TryParse(bs.GetAttribute("fermentingTime", "").Replace('.', ','), out thisBunker.fermentingTime);

                        // Also combine the id with the poit index for each pit.
                        allBunkerSiloes.Add(BunkerSiloId + "_"+bs.GetAttribute("index", ""), thisBunker); 
                    }
                }


                // Animals
                if (item.GetAttribute("className", "") == "AnimalHusbandry" && item.GetAttribute("farmId", "") == config.FarmID.ToString())
                {
                    AnimalHusbandry thisAnimalHusbandry = new AnimalHusbandry();

                    // We use position to make an ID
                    string AnimalHusbandryPosition = item.GetAttribute("position", "");
                    string firstCoord = AnimalHusbandryPosition.Split(' ')[0].Split('.')[0];
                    int.TryParse(firstCoord, out int AnimalHusbandryId);
                    AnimalHusbandryId = Math.Abs(AnimalHusbandryId);

                    // Get all the modules
                    XPathNodeIterator modules = item.Select("module");
                    foreach (XPathNavigator module in modules)
                    {
                        // NOTE: manure is not possible to track in this way, because it is not stored in the save file as part of the Husbandry building. 
                        switch (module.GetAttribute("name",""))
                        {
                            case "milk":
                                float.TryParse(module.SelectSingleNode("fillLevel").GetAttribute("fillLevel", "").Replace('.',','),out thisAnimalHusbandry.milk);
                                break;
                            case "liquidManure":
                                float.TryParse(module.SelectSingleNode("fillLevel").GetAttribute("fillLevel", "").Replace('.', ','), out thisAnimalHusbandry.slurry);
                                break;
                            case "animals":
                                XPathNodeIterator animals = module.Select("animal");
                                thisAnimalHusbandry.animal = animals.Count;
                                break;

                            default:
                                break;
                        }

                    }
                    allAnimalHusbandry.Add(AnimalHusbandryId.ToString(), thisAnimalHusbandry);
                }
            }

            // Get the price of things from the seasons historical plot
            XPathNavigator priceHistory = seasonsNav.SelectSingleNode("/seasons/economy/history");

            XPathNodeIterator fillTypes = priceHistory.Select("fill");

            Dictionary<string, float[]> historyPrices = new Dictionary<string, float[]>();

            foreach(XPathNavigator fill in fillTypes)
            {
                string fillTypeKey = fill.GetAttribute("fillType", "");
                string[] stringValues = fill.SelectSingleNode("values").Value.Replace('.', ',').Split(';');
                float[] values = new float[stringValues.Length];

                for (int i = 0; i < stringValues.Length; i++)
                {
                    float.TryParse(stringValues[i], out values[i]);
                }

                historyPrices.Add(fillTypeKey, values);
            }

            // Setup InfluxDBClient
            // This is a lazy client and does not connect untill you ask it to actually do something.
            InfluxDbClient influxClient = new InfluxDbClient(
                config.GetInfluxEndpoint(),
                config.InfluxUser,
                config.InfluxPassword,
                InfluxDbVersion.Latest);

            List<Point> point = new List<Point>();

            foreach (string key in allSilos.Keys)
            {
                Point thisPoint = new Point();
                thisPoint.Name = "siloLevels";
                thisPoint.Tags = new Dictionary<string, object>()
                {
                    {"savegameName", savegameName },
                    {"mapTitle", mapTitle },
                    {"fillType", key }
                };
                thisPoint.Fields = new Dictionary<string, object>()
                {
                    {"level",allSilos[key] },
                    {"price",historyPrices[key][dayInYear-1] }
                };
                point.Add(thisPoint);
            }
            foreach (string key in allBunkerSiloes.Keys)
            {
                Point thisPoint = new Point();
                thisPoint.Name = "bunkerSiloes";
                thisPoint.Tags = new Dictionary<string, object>()
                {
                    { "id", key},
                    {"savegameName", savegameName },
                    {"mapTitle", mapTitle }
                };
                thisPoint.Fields = new Dictionary<string, object>()
                {
                    { "fillLevel", allBunkerSiloes[key].fillLevel },
                    { "compactedFillLevel", allBunkerSiloes[key].compactedFillLevel },
                    { "fermentingTime", allBunkerSiloes[key].fermentingTime },
                    { "state", allBunkerSiloes[key].state }
                };

                point.Add(thisPoint);
            }
            foreach (string key in allAnimalHusbandry.Keys)
            {
                Point thisPoint = new Point();
                thisPoint.Name = "animals";
                thisPoint.Tags = new Dictionary<string, object>()
                {
                    {"id",key },
                    {"savegameName", savegameName },
                    {"mapTitle", mapTitle }
                };
                thisPoint.Fields = new Dictionary<string, object>()
                {
                    { "milk", allAnimalHusbandry[key].milk },
                    { "slurry", allAnimalHusbandry[key].slurry },
                    { "animals", allAnimalHusbandry[key].animal }
                };
                point.Add(thisPoint);
            }

            Point daypoint = new Point();
            daypoint.Name = "days";
            daypoint.Tags = new Dictionary<string, object>()
                {
                    {"savegameName", savegameName },
                    {"mapTitle", mapTitle }
                };
            daypoint.Fields = new Dictionary<string, object>()
                {
                    { "daysPerSeason", daysPerSeason },
                    { "dayInYear", dayInYear },
                    { "currentDay", currentDay },
                    { "daysInYear", daysInYear },
                    { "seasonsDay", seasonsDay }
                };
            point.Add(daypoint);

            var result = influxClient.Client.WriteAsync(point, "farmingsim").GetAwaiter().GetResult();
            
        }
        public class Bunker
        {
            public float fillLevel;
            public float compactedFillLevel;
            public int state;
            public float fermentingTime;
        }

        public class AnimalHusbandry
        {
            public float milk;
            public int animal;
            public float slurry;

        }

        public static void CheckForConfigFile()
        {
            if (File.Exists("config.xml"))
            {
                return;
            }

            Console.WriteLine("Bootstrapping config..");

            // Make a config file
            Configuration bootStrapConfig = new Configuration();

            // Make a streamwriter
            StreamWriter xmlConfig = new StreamWriter("config.xml");

            // XML serializer
            XmlSerializer configSerializer = new XmlSerializer(typeof(Configuration));
            configSerializer.Serialize(xmlConfig, bootStrapConfig);
            xmlConfig.Close();
            Environment.Exit(0);
        }

        public static Configuration ReadConfiguration()
        {
            // Make a streamwriter
            Stream xmlConfig = new FileStream("config.xml", FileMode.Open);

            // XML serializer
            XmlSerializer configSerializer = new XmlSerializer(typeof(Configuration));

            Configuration result = (Configuration)configSerializer.Deserialize(xmlConfig);
            return result;
        }
    }
}
