using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using PassiveListening.Resources;
using Windows.Phone.Speech.Recognition;
using System.Threading;

namespace PassiveListening
{
    public partial class MainPage : PhoneApplicationPage
    {
        // Hotword & Secondary listeners
        private SpeechRecognizer hotWordListener, secondaryInputListener;

        // Results for Hotword & Secondary recognitions
        private SpeechRecognitionResult hotWordResult, secondaryResult;

        // Set the hotword here
        private const String HOTWORD = "start";

        // Defines the speech accuracy for the hotword detection
        private const SpeechRecognitionConfidence SPEECH_ACCURACY = SpeechRecognitionConfidence.Medium;

        // Passiver runner thread to keep the hotword recognition going
        private Thread passiveRunner = null;

        // Constructor
        public MainPage()
        {
            InitializeComponent();
            
            // Set up the button handler
            btnActiveListening.Click += btnActiveListening_Click;

            initializeItems();

        }

        async void initializeItems()
        {
            // Set up the speech listeners & wire up the list of words
            if (hotWordListener == null)
            {
                hotWordListener = new SpeechRecognizer();
                secondaryInputListener = new SpeechRecognizer();

                hotWordListener.Grammars.AddGrammarFromList("hotword", new List<String>() { HOTWORD });
                secondaryInputListener.Grammars.AddGrammarFromList("words", new List<String>() { "hello", "welcome", "world" });

                await hotWordListener.PreloadGrammarsAsync();
                await secondaryInputListener.PreloadGrammarsAsync();
            }

            if (passiveRunner == null)
            {
                passiveRunner = new Thread(new ThreadStart(startListeningForHotword));
                passiveRunner.IsBackground = true;
            }

        }

        /*
         * This is the function which does the Passive Listening. It's a self calling function, 
         * which loops infinitely till the thread it is running in is stopped.
         */
        async void startListeningForHotword()
        {
            // Update the status on UI
            postMessageOnUI(  lblActiveListeningCurrentStatus, "Listening for Hotword ...");

            // Check for listener
            if(hotWordListener == null)
            { return;  }

            // Start listening for the trigger keyword ("hotword")
            hotWordResult = await hotWordListener.RecognizeAsync();
            
            // Update the status on UI
            postMessageOnUI(  lblActiveListeningCurrentStatus, "Recognition Timed Out ...");

            // If the hotword result is not null
            if (hotWordResult != null)
            {
                // Check for the minimum accuracy so that we don't pick up garbage input
                if (hotWordResult.TextConfidence >= SPEECH_ACCURACY)
                {
                    // We have made it through the hotword detection, we can initialize the secondary listening now

                    // Update the status on UI
                    postMessageOnUI(  lblActiveListeningCurrentStatus, "Hotword DETECTED, Awaiting secondary input ...");

                    // Get the secondary input
                    secondaryResult = await secondaryInputListener.RecognizeAsync();

                    if (secondaryResult.TextConfidence != SpeechRecognitionConfidence.Rejected)
                    {
                        postMessageOnUI( lblSecondaryInputStatus,"Secondary input detected. Input is \"" + secondaryResult.Text + "\"");
                    }
                    else
                    {
                        postMessageOnUI( lblSecondaryInputStatus,"Failed getting secondary input!");
                    }
                }
            }
            else
            {
                postMessageOnUI( lblActiveListeningCurrentStatus,  "Recognition failed, restarting Hotword recognition ...");
            }

            startListeningForHotword();
        }

        /*
         * Function to post on the UI thread from the secondary thread
         */
        void postMessageOnUI(TextBlock control, String message)
        {
            Dispatcher.BeginInvoke(() =>
            {
                control.Text = message;
            });
        }

        void btnActiveListening_Click(object sender, RoutedEventArgs e)
        {
            if (btnActiveListening.Content.ToString().Contains("Start"))
            {
                lblActiveListeningCurrentStatus.Text = "Listening for Hotword ... ";
                lblActiveListeningStatus.Text = "Active";

                // Disable idle detection to run behind the lock screen as well
                PhoneApplicationService.Current.ApplicationIdleDetectionMode = IdleDetectionMode.Disabled;
                try
                {
                    passiveRunner.Start();
                    btnActiveListening.Content = "Stop Passive Listening";
                }
                catch (Exception)
                {
                }
            }
            else
            {
                try
                {
                    passiveRunner.Abort();
                    passiveRunner = null;
                    hotWordListener = null;
                    lblActiveListeningStatus.Text = "Stopped";
                    lblActiveListeningCurrentStatus.Text = "Idle";
                    btnActiveListening.Content = "Start Passive Listening";
                }
                catch (Exception)
                {

                }
                btnActiveListening.IsEnabled = false;
            }
        }
    }
}