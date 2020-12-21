using System;
using System.Collections.Generic;
using System.Text;

namespace FarmingSimToInflux
{
    /// <summary>
    /// Holds the configuration for the program.
    /// Is public to simplify serilization.
    /// </summary>
    public class Configuration
    {
        /// <summary>
        /// Gets or Sets the savegamefolder.
        /// </summary>
        public string SaveGameFolder { get; set; } = @"C:\Users\Kjell\Documents\My Games\FarmingSimulator2019\savegame1\";

        /// <summary>
        /// Gets or Sets the farmID.
        /// Userfull for multiplayer saves. Defaults to 1 in singleplayer maps.
        /// </summary>
        public int FarmID { get; set; } = 1;

        /// <summary>
        /// Gets or Sets the influx database name.
        /// </summary>
        public string InfluxDatabase { get; set; } = "farmingsim";

        /// <summary>
        /// Gets or Sets the influx host.
        /// </summary>
        public string InfluxHost { get; set; } = "localhost";

        /// <summary>
        /// Gets or Sets the influx port.
        /// </summary>
        public string InfluxPort { get; set; } = "8086";

        /// <summary>
        /// Gets or Sets Influx user.
        /// </summary>
        public string InfluxUser { get; set; } = "user";

        /// <summary>
        /// Gets or Sets the influx password.
        /// </summary>
        public string InfluxPassword { get; set; } = "password";

        /// <summary>
        /// Gets the influx enpoint string.
        /// </summary>
        /// <returns>An influx http endpoint.</returns>
        public string GetInfluxEndpoint()
        {
            return "http://" + this.InfluxHost + ":" + this.InfluxPort + "/";
        }
    }
}
