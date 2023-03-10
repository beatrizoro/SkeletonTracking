//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using System.Windows.Automation.Peers;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using System.Windows.Shell;
    using Microsoft.Kinect;
    using static System.Net.Mime.MediaTypeNames;
    using System.Globalization;
    using System.Windows.Documents;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        /// 

        //varibale
        public bool trackerVar = false;
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
               lblKinectStatus.Content = Properties.Resources.NoKinectReady;
           }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                            this.getInfo(skel); //getting info
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);
            
            
            //print Info onto Screen
            //this.getInfo(skeleton);

            //print Info into txt file

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                   
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;                    
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }

        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        private void writeInfo(string []info, string activityName)
        {
            //string[] info = new string[150]; //80 data points - 1 label, - state
            int i = 0;
            //DepthImagePoint jointDepth;
            //foreach (Joint joint in skeleton.Joints)
            //{
            //    info[i] = joint.Position.X.ToString("0.00");
            //    info[i+1] = joint.Position.Y.ToString("0.00");
            //    info[i+2] = joint.Position.Z.ToString("0.00");
            //    jointDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(joint.Position, DepthImageFormat.Resolution640x480Fps30);
            //    info[i + 3] = jointDepth.Depth.ToString("0.00");
            //    i +=4;
            //}

            string infoline = "";
            for (i =0; i< info.Length; i++)
            {
                if (i == info.Length -1)
                {
                    infoline += info[i] + "\n";
                }
                else
                {
                    infoline += info[i] + ",";
                } 
               
            }
  
            File.AppendAllText(activityName+".txt", infoline);
        }
        private void getInfo(Skeleton skeleton)
        {
            string[] info = new string[101];
            int i = 1;
            info[0] = DateTime.Now.ToString();
            DepthImagePoint jointDepth;
            foreach (Joint joint in skeleton.Joints)
            {
                
                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                
                    //info[i] = joint.TrackingState.ToString();//
                    info[i] = joint.Position.X.ToString();
                    info[i + 1] = joint.Position.Y.ToString();
                    info[i + 2] = joint.Position.Z.ToString();
                    jointDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(joint.Position, DepthImageFormat.Resolution640x480Fps30);
                    info[i + 3] = jointDepth.Depth.ToString();
                    i += 4;
                }
                else
                {
                  
                    //info[i] = joint.TrackingState.ToString();// 
                    info[i] = "NA";
                    info[i + 1] = "NA";
                    info[i + 2] = "NA";
                    jointDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(joint.Position, DepthImageFormat.Resolution640x480Fps30);
                    info[i + 3] = "NA";
                    i += 4;
                }
            }
            if(trackerVar == true)
            {
                //writeCols(txtActivityName.Text);
                writeInfo(info,txtActivityName.Text); 
            }
            
            Position.Text =
                "X Position: " + string.Format("{0:N2}", skeleton.Position.X) + "\n"
                + "Y Position: " + string.Format("{0:N2}", skeleton.Position.Y) + "\n"
                + "Z Position: " + string.Format("{0:N2}", skeleton.Position.Z) + "\n"

                + "-------------------------------------------------" + "\n"
                + "Head (X,Y,Z): (" + info[0] + "," + info[1] + "," + info[2] + ")\n"

                + "ShoulderCenter (X,Y,Z): (" + info[4] + "," + info[5] + "," + info[6] + ")\n"
                + "ShoulderLeft (X,Y,Z): (" + info[8] + "," + info[9] + "," + info[10] + ")\n"
                + "ShoulderRight (X,Y,Z): (" + info[12] + "," + info[13] + "," + info[14] + ")\n"

                + "Spine Position (X,Y,Z): (" + info[12] + "," + info[13] + "," + info[14] + ")\n"

                 + "HipCenter (X,Y,Z): (" + info[12] + "," + info[13] + "," + info[14] + ")\n" 
                + "HipLeft (X,Y,Z): (" + info[12] + "," + info[13] + "," + info[14] + ")\n"
                + "HipRight (X,Y,Z): (" + info[12] + "," + info[13] + "," + info[14] + ")\n"


                + "ElbowLeft (X,Y,Z): (" + info[12] + "," + info[13] + "," + info[14] + ")\n"
                + "WristLeft (X,Y,Z): (" + skeleton.Joints[JointType.WristLeft].Position.X.ToString("0.00") + "," + skeleton.Joints[JointType.WristLeft].Position.Y.ToString("0.00") + "," + skeleton.Joints[JointType.WristLeft].Position.Z.ToString("0.00") + ")\n"
                + "HandLeft (X,Y,Z): (" + skeleton.Joints[JointType.HandLeft].Position.X.ToString("0.00") + "," + skeleton.Joints[JointType.HandLeft].Position.Y.ToString("0.00") + "," + skeleton.Joints[JointType.HandLeft].Position.Z.ToString("0.00") + ")\n"



                + "ElbowRight (X,Y,Z): (" + skeleton.Joints[JointType.ElbowRight].Position.X.ToString("0.00") + skeleton.Joints[JointType.ElbowRight].Position.Y.ToString("0.00") + "," + skeleton.Joints[JointType.ElbowRight].Position.Z.ToString("0.00") + ")\n"
                + "WristRight (X,Y,Z): (" + skeleton.Joints[JointType.WristRight].Position.X.ToString("0.00") + skeleton.Joints[JointType.WristRight].Position.Y.ToString("0.00") + "," + skeleton.Joints[JointType.WristRight].Position.Z.ToString("0.00") + ")\n"
                + "HandRight (X,Y,Z): (" + skeleton.Joints[JointType.HandRight].Position.X.ToString("0.00") + skeleton.Joints[JointType.HandRight].Position.Y.ToString("0.00") + "," + skeleton.Joints[JointType.HandRight].Position.Z.ToString("0.00") + ")\n"


                + "KneeLeft (X,Y,Z): (" + skeleton.Joints[JointType.KneeLeft].Position.X.ToString("0.00") + skeleton.Joints[JointType.KneeLeft].Position.Y.ToString("0.00") + "," + skeleton.Joints[JointType.KneeLeft].Position.Z.ToString("0.00") + ")\n"
                + "AnkleLeft (X,Y,Z): (" + skeleton.Joints[JointType.AnkleLeft].Position.X.ToString("0.00") + skeleton.Joints[JointType.AnkleLeft].Position.Y.ToString("0.00") + "," + skeleton.Joints[JointType.AnkleLeft].Position.Z.ToString("0.00") + ")\n"
                + "FootLeft (X,Y,Z): (" + skeleton.Joints[JointType.FootLeft].Position.X.ToString("0.00") + skeleton.Joints[JointType.FootLeft].Position.Y.ToString("0.00") + "," + skeleton.Joints[JointType.FootLeft].Position.Z.ToString("0.00") + ")\n"

                 + "KneeRight (X,Y,Z): (" + skeleton.Joints[JointType.KneeRight].Position.X.ToString("0.00") + skeleton.Joints[JointType.KneeRight].Position.Y.ToString("0.00") + "," + skeleton.Joints[JointType.KneeRight].Position.Z.ToString("0.00") + ")\n"
                + "AnkleRight (X,Y,Z): (" + skeleton.Joints[JointType.AnkleRight].Position.X.ToString("0.00") + skeleton.Joints[JointType.AnkleRight].Position.Y.ToString("0.00") + "," + skeleton.Joints[JointType.AnkleRight].Position.Z.ToString("0.00") + ")\n"
                + "FootRight (X,Y,Z): (" + skeleton.Joints[JointType.FootRight].Position.X.ToString("0.00") + skeleton.Joints[JointType.FootRight].Position.Y.ToString("0.00") + "," + skeleton.Joints[JointType.FootRight].Position.Z.ToString("0.00") + ")\n"

               + "Leg Length: " + (skeleton.Joints[JointType.HipLeft].Position.X - skeleton.Joints[JointType.AnkleLeft].Position.X).ToString("0.00")
              
                ;

            DepthImagePoint headDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.Head].Position, DepthImageFormat.Resolution640x480Fps30);

            DepthImagePoint shouldercenterDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.ShoulderCenter].Position, DepthImageFormat.Resolution640x480Fps30);
            DepthImagePoint shoulderleftDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.ShoulderLeft].Position, DepthImageFormat.Resolution640x480Fps30);
            DepthImagePoint shoulderrightDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.ShoulderRight].Position, DepthImageFormat.Resolution640x480Fps30);

            DepthImagePoint spineDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.Spine].Position, DepthImageFormat.Resolution640x480Fps30);

            DepthImagePoint hipcenterDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.HipCenter].Position, DepthImageFormat.Resolution640x480Fps30);
            DepthImagePoint hipleftDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.HipLeft].Position, DepthImageFormat.Resolution640x480Fps30);
            DepthImagePoint hiprightDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.HipRight].Position, DepthImageFormat.Resolution640x480Fps30);

            DepthImagePoint elbowleftDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.ElbowLeft].Position, DepthImageFormat.Resolution640x480Fps30);
            DepthImagePoint wristleftDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.WristLeft].Position, DepthImageFormat.Resolution640x480Fps30);
            DepthImagePoint handleftDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.HandLeft].Position, DepthImageFormat.Resolution640x480Fps30);

            DepthImagePoint elbowrightDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.Head].Position, DepthImageFormat.Resolution640x480Fps30);
            DepthImagePoint wristrightDepth= this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.Head].Position, DepthImageFormat.Resolution640x480Fps30);
            DepthImagePoint handrightDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.Head].Position, DepthImageFormat.Resolution640x480Fps30);
            
            DepthImagePoint kneeleftDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.Head].Position, DepthImageFormat.Resolution640x480Fps30);
            DepthImagePoint ankleleftDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.Head].Position, DepthImageFormat.Resolution640x480Fps30);
            DepthImagePoint footleftDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.Head].Position, DepthImageFormat.Resolution640x480Fps30);
            
            DepthImagePoint kneerightDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.Head].Position, DepthImageFormat.Resolution640x480Fps30);
            DepthImagePoint anklerightDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.Head].Position, DepthImageFormat.Resolution640x480Fps30);
            DepthImagePoint footrightDepth = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skeleton.Joints[JointType.Head].Position, DepthImageFormat.Resolution640x480Fps30);
            




            Depth.Text = "Head Depth: "+ headDepth.Depth.ToString("0.00") + "\n"

               + "Shoulder Center Depth: " + shouldercenterDepth.Depth.ToString("0.00") + "\n"
               + "Shoulder L Depth: " + shoulderleftDepth.Depth.ToString("0.00") + "\n"
               + "SHoulder R Depth: " + shoulderrightDepth.Depth.ToString("0.00") + "\n"

               + "Spine Depth: " + spineDepth.Depth.ToString("0.00") + "\n"

               + "Hip Center Depth: " + hipcenterDepth.Depth.ToString("0.00") + "\n"
               + "Hip L Depth: " + hipleftDepth.Depth.ToString("0.00") + "\n"
               + "Hip R Depth: " + hiprightDepth.Depth.ToString("0.00") + "\n"

               + "Elbow R Depth: " + elbowrightDepth.Depth.ToString("0.00") + "\n"
               + "Wrist R Depth: " + wristrightDepth.Depth.ToString("0.00") + "\n"
               + "Hand R Depth: " + handrightDepth.Depth.ToString("0.00") + "\n"
               
               + "Elbow L Depth: " + elbowleftDepth.Depth.ToString("0.00") + "\n"
               + "Wrist L Depth: " + wristleftDepth.Depth.ToString("0.00") + "\n"
               + "Hand L Depth: " + handleftDepth.Depth.ToString("0.00") + "\n"

               + "Knee R Depth: " + kneerightDepth.Depth.ToString("0.00") + "\n"
               + "Ankle R Depth: " + anklerightDepth.Depth.ToString("0.00") + "\n"
               + "Foot R Depth: " + footrightDepth.Depth.ToString("0.00") + "\n"

               + "Knee L Depth: " + kneeleftDepth.Depth.ToString("0.00") + "\n"
               + "Ankle L Depth: " + ankleleftDepth.Depth.ToString("0.00") + "\n"
               + "Foot L Depth: " + footleftDepth.Depth.ToString("0.00") + "\n"

                ;
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
/*
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }
*/
        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (trackerVar ==false)
            {
                trackerVar = true;
                //writeCols(txtActivityName.Text);
                lblTrackingState.Content = "Tracking";
                
            }
            else
            {
                trackerVar = false;
                lblTrackingState.Content = "Not Tracking";
            }
        }


        private void writeCols(string fileName)
        {
            string[] columnName = new string[81];

            columnName[0] = "DateTime";

            columnName[1] = "HipCenter X";
            columnName[2] = "HipCenter Y";
            columnName[3] = "HipCenter Z";
            columnName[4] = "HipCenter Depth";


            columnName[5] = "Spine X";
            columnName[6] = "Spine Y";
            columnName[7] = "Spine Z";
            columnName[8] = "Spine Depth";


            columnName[9] = "ShoulderCenter X";
            columnName[10] = "ShoulderCenter Y";
            columnName[11] = "ShoulderCenter Z";
            columnName[12] = "ShoulderCenter Depth";


            columnName[13] = "Head X";
            columnName[14] = "Head Y";
            columnName[15] = "Head Z";
            columnName[16] = "Head Depth";


            columnName[17] = "ShoulderLeft X";
            columnName[18] = "ShoulderLeft Y";
            columnName[19] = "ShoulderLeft Z";
            columnName[20] = "ShoulderLeft Depth";


            columnName[21] = "ElbowLeft X";
            columnName[22] = "ElbowLeft Y";
            columnName[23] = "ElbowLeft Z";
            columnName[24] = "ElbowLeft Depth";


            columnName[25] = "WristLeft X";
            columnName[26] = "WristLeft Y";
            columnName[27] = "WristLeft Z";
            columnName[28] = "WristLeft Depth";

            columnName[29] = "HandLeft X";
            columnName[30] = "HandLeft Y";
            columnName[31] = "HandLeft Z";
            columnName[32] = "HandLeft Depth";


            columnName[33] = "ShoulderRight X";
            columnName[34] = "ShoulderRight Y";
            columnName[35] = "ShoulderRight Z";
            columnName[36] = "ShoulderRight Depth";




            columnName[37] = "ElbowRight X";
            columnName[38] = "ElbowRight Y";
            columnName[39] = "ElbowRight Z";
            columnName[40] = "ElbowRight Depth";


            columnName[41] = "WristRight X";
            columnName[42] = "WristRight Y";
            columnName[43] = "WristRight Z";
            columnName[44] = "WristRight Depth";




            columnName[45] = "HandRight X";
            columnName[46] = "HandRight Y";
            columnName[47] = "HandRight Z";
            columnName[48] = "HandRight Depth";




            columnName[49] = "HipLeft X";
            columnName[50] = "HipLeft Y";
            columnName[51] = "HipLeft Z";
            columnName[52] = "HipLeft Depth";




            columnName[53] = "KneeLeft X";
            columnName[54] = "KneeLeft Y";
            columnName[55] = "KneeLeft Z";
            columnName[56] = "KneeLeft Depth";




            columnName[57] = "AnkleLeft X";
            columnName[58] = "AnkleLeft Y";
            columnName[59] = "AnkleLeft Z";
            columnName[60] = "AnkleLeft Depth";




            columnName[61] = "FootLeft X";
            columnName[62] = "FootLeft Y";
            columnName[63] = "FootLeft Z";
            columnName[64] = "FootLeft Depth";




            columnName[65] = "HipRight X";
            columnName[67] = "HipRight Y";
            columnName[68] = "HipRight Z";
            columnName[69] = "HipRight Depth";




            columnName[70] = "KneeRight X";
            columnName[71] = "KneeRight Y";
            columnName[72] = "KneeRight Z";
            columnName[73] = "KneeRight Depth";




            columnName[74] = "AnkleRight X";
            columnName[75] = "AnkleRight Y";
            columnName[76] = "AnkleRight Z";
            columnName[77] = "AnkleRight Depth";




            columnName[78] = "FootRight X";
            columnName[79] = "FootRight Y";
            columnName[80] = "FootRight Z";
            columnName[81] = "FootRight Depth";

            





            string columns = "";
            for (int i = 0; i < columnName.Length; i++)
            {
                if (i == columnName.Length - 1)
                {
                    columns += columnName[i] + "\n";
                }
                else
                {
                    columns += columnName[i] + ",";
                }

            }
            File.AppendAllText(fileName + ".txt", columns);
        }
    }
}