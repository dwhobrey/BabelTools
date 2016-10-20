README file for BabelTools
**************************
Version: 0.1.0.0, 2013.

Overview of BabelTools
======================
BabelTools is an experimental software system for testing and controlling embedded devices
via multiplexed communication links, such as USB, RS232, BLE, WiFi, or Ethernet.

The core of BabelTools is an MDI framework for hosting modules (plugins).
A module interface is provided that makes it easy to wrap pre-exisiting software components.
Scripting is used to glue modules together. 

For example, the motion control module was used to automate some special effects
in the recent Warner Brothers, Peter Pan movie.

The inbuilt modules include a view handler, project organiser, and license manager.

Inbuilt views include a JavaScript scripting shell (using Jint), 
text editor (using Avalon), file explorer, HTML viewer, 
and chart view (using OxyPlot), among others.

Secondary modules include:
A) Link modules for handling I/O, such as COM and USB.
B) Protocol modules, currently just the Babel Protocol module.
C) An I/O exchange (part of the protocol module).
D) A motion control and recording module.
E) Some experimental modules still under construction, 
   such as a Dashboard designer (using Sukram's as a basis).

BabelTools is written mostly in C# with a few components written in C/C++.

BabelFish is an example application that fires up the MDI framework and loads in any modules it finds.

See the html help files for a particular module.
+++

