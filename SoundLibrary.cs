using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Media;

namespace Pi_the_Robot_test {
    public class Sound_out {

        //***********************************************************************
        // Objects
        //***********************************************************************

        private SoundPlayer player;

        // Sets up the SoundPlayer object.
        private void InitializeSound()
        {
            // Create an instance of the SoundPlayer class.
            player = new SoundPlayer();
        }

    }
}
