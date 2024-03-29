﻿


/*
LOLViewer
Copyright 2011-2012 James Lammlein 

 

This file is part of LOLViewer.

LOLViewer is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
any later version.

LOLViewer is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with LOLViewer.  If not, see <http://www.gnu.org/licenses/>.

*/

//
// Extracts model and texture information
// from the League of Legends directory
// structure.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

using RAFlibPlus;

namespace LOLViewer.IO
{
    public class LOLDirectoryReader
    {
        public const String DEFAULT_ROOT = "C:/Riot Games/League of Legends";
        public const String DEFAULT_MODEL_ROOT = "/DATA/Characters";
        public const String DEFAULT_RAF_DIRECTORY_ONE = "DATA";
        public const String DEFAULT_RAF_DIRECTORY_TWO = "Characters";

        public const String DEFAULT_EXTRACTED_TEXTURES_ROOT = "content/textures/";
        public String root;

        public Dictionary<String, RAFFileListEntry> skls;
        public Dictionary<String, RAFFileListEntry> skns;
        public Dictionary<String, RAFFileListEntry> textures;

        public List<RAFFileListEntry> inibins;
        public Dictionary<String, RAFFileListEntry> animationLists;
        public Dictionary<String, RAFFileListEntry> animations;


        public Dictionary<String, LOLModel> models;

        public LOLDirectoryReader()
        {
            root = DEFAULT_ROOT;

            inibins = new List<RAFFileListEntry>();

            animationLists = new Dictionary<String, RAFFileListEntry>();
            animations = new Dictionary<String, RAFFileListEntry>();
            
            skls = new Dictionary<String, RAFFileListEntry>();
            skns = new Dictionary<String, RAFFileListEntry>();
            textures = new Dictionary<String, RAFFileListEntry>();

            models = new Dictionary<String,LOLModel>();
        }

        /// <summary>
        /// Call this if LOL was installed in a non-default location.
        /// </summary>
        /// <param name="s">Full path to and including the "Riot Games" folder.</param>
        public void SetRoot(String s)
        {
            root = s;
        }

        public bool Read( EventLogger logger )
        {
            bool result = true;

            // Clear old data.

            models.Clear();

            skls.Clear();
            skns.Clear();
            textures.Clear();

            inibins.Clear();
            animationLists.Clear();
            animations.Clear();           

            DirectoryInfo rootDir = null;
            try
            {
                logger.LogEvent("Reading models from: " + root);
                rootDir = new DirectoryInfo(root);
            }
            catch 
            {
                logger.LogError("Unable to get the directory information: " + root);
                return false;
            }

            //
            // Directory Guard.
            // Assume the user selected the wrong LOL installation directory.
            //
            // Possibly correct directories:
            // - Name contains "Riot Games"  ...\Riot Games\League of Legends\RADS...
            // - Name contains "League of Legends" ...\League of Legends\RADS...
            // - Child Directory Contains "League of Legends" ...\Renamed\League of Legends\RADS...
            // - Child Directory Contains "RADS" ...\Renamed\RADS...
            //
            // The only case not really handled is something like
            // ...\Riot Games\Renamed\RADS... which no one better have!  And, honestly, this case should fail
            // because someone could have a new Riot game installed which is not LOL.
            //

            bool isRootSelected = false;

            // Deafult directory installations.
            if (rootDir.Name.Contains("League of Legends") == true ||
                rootDir.Name.Contains("Riot Games") == true ||
                rootDir.Name.Contains("RADS") == true ||
                rootDir.Name.Contains("rads") == true )
            {
                isRootSelected = true;
            }
            // Selected a renamed "Riot Games" directory.
            else if (ContainsDirectory(rootDir, "League of Legends") == true)
            {
                isRootSelected = true;
            }
            // Selected a rename "League of Legends" directory.
            else if (ContainsDirectory(rootDir, "RADS") == true || 
                     ContainsDirectory(rootDir, "rads") == true )
            {
                isRootSelected = true;
            }

            // If we didn't find the root, just bail.
            if (isRootSelected == false)
            {
                logger.LogError(root + " is not a League of Legends root directory.");
                return false;
            }

            //
            // Start from the root and try to read
            // model files and textures.
            //
            try
            {
                DirectoryInfo di = new DirectoryInfo(root);
                foreach (DirectoryInfo d in di.GetDirectories())
                {
                    result = ReadDirectory(d, logger);
                }
            }
            catch(Exception e)
            {
                logger.LogError("Unable to open directory: " + root);
                logger.LogError(e.Message);
                result = false;
            }

            foreach( RAFFileListEntry f in inibins )
            {
                InibinFile iniFile = new InibinFile();
                bool readResult = InibinReader.ReadCharacterInibin(f, ref iniFile, logger);

                if (readResult == true)
                {
                    // Add the models from this .inibin file
                    List<ModelDefinition> modelDefs = iniFile.GetModelStrings();
                    for (int j = 0; j < modelDefs.Count; ++j)
                    {
                        // Name the model after the parent directory
                        // of the .inibin plus the name from the .inibin.
                        // Some things overlap without both.
                        String name = modelDefs[j].name;

                        String directoryName = f.FileName;
                        int pos = directoryName.LastIndexOf("/");
                        directoryName = directoryName.Remove(pos);
                        pos = directoryName.LastIndexOf("/");
                        directoryName = directoryName.Substring(pos + 1);

                        // Sometimes the name from the .inibin file is "".
                        // So, just name it after the directory
                        if (name == "")
                        {
                            name = directoryName + "/" + directoryName;
                        }
                        else
                        {
                            name = directoryName + "/" + name;
                        }

                        try
                        {
                            LOLModel model;
                            bool storeResult = StoreModel(modelDefs[j], out model, logger);

                            if (storeResult == true)
                            {
                                // Try to store animations for model as well
                                storeResult = StoreAnimations(ref model, logger);
                            }

                            if (storeResult == true)
                            {
                                if (models.ContainsKey(name) == false)
                                {
                                    logger.LogEvent("Adding model definition: " + name);
                                    models.Add(name, model);
                                }
                                else
                                {
                                    logger.LogWarning("Duplicate model definition: " + name);
                                }
                            }
                        }
                        catch(Exception e) 
                        {
                            logger.LogError("Unable to store model definition: " + name);
                            logger.LogError(e.Message);
                        }
                    }
                }
            }

            return result;
        }

        private bool StoreModel(ModelDefinition def, out LOLModel model, EventLogger logger)
        {
            model = new LOLModel();
            model.skinNumber = def.skin;
            model.animationList = def.anmListKey.ToLower();

            // Find the skn.
            if (skns.ContainsKey(def.skn))
            {
                model.skn = skns[def.skn];
            }
            else
            {
               logger.LogError("Unable to find skn file: " + skns[def.skn]);
               return false;
            }

            // Find the skl.
            if (skls.ContainsKey(def.skl))
            {
                model.skl = skls[def.skl];
            }
            else
            {
                logger.LogError("Unable to find skl file: " + skls[def.skl]);
                return false;
            }

            // Find the texture.
            if (textures.ContainsKey(def.tex))
            {
                model.texture = textures[def.tex];
            }
            else
            {
                logger.LogError("Unable to find texture file: " + textures[def.tex].FileName);
                return false;
            }

            return true;
        }

        private bool StoreAnimations(ref LOLModel model, EventLogger logger)
        {
            bool result = true;

            Dictionary<String, String> animationStrings =
                new Dictionary<String, String>();

            // Sanity
            if (animationLists.ContainsKey(model.animationList) == true)
            {
                result = ANMListReader.ReadAnimationList(model.skinNumber - 1, // indexing in animations.list assumes the original skin to be -1
                    animationLists[model.animationList], ref animationStrings, logger);
            }
            else
            {
                logger.LogError("Unable to find animation list: " + model.animationList);
            }

            if (result == true)
            {
                // Store the animations in the model.
                foreach (var a in animationStrings)
                {
                    if (animations.ContainsKey(a.Value) == true )
                    {
                        if( model.animations.ContainsKey(a.Key) == false )
                        {
                            model.animations.Add(a.Key, animations[a.Value]);
                        }
                        else
                        {
                            logger.LogError("Duplicate animation: " + a.Key);
                        }
                    }
                    else
                    {
                        logger.LogError("Unable to find animation: " + a.Value);
                    }
                }
            }

            return result;
        }

        public void SortModelNames()
        {
            IEnumerable<KeyValuePair<String, LOLModel>> alphabetical = models.OrderBy(model => model.Key);

            Dictionary<String, LOLModel> temp = new Dictionary<String, LOLModel>();
            foreach (var m in alphabetical)
            {
                temp.Add(m.Key, m.Value);
            }

            models.Clear();
            models = temp;
        }

        public List<String> GetModelNames()
        {
            List<String> names = new List<String>();
            
            foreach (var model in models)
            {
                names.Add(model.Key);
            }

            return names;
        }

        public LOLModel GetModel(String name)
        {
            LOLModel result = null;
            
            foreach(var m in models)
            {
                if (m.Key == name)
                {
                    // This is the model we want.
                    result = m.Value;
                    break;
                }
            }

            return result;
        }

        //
        // Helper functions for reading the directory structure.
        //

        private bool ReadDirectory(DirectoryInfo dir, EventLogger logger)
        {
            bool result = true;

            //
            // Parse the directory's name and determine what to do.
            //

            // Odd case
            // US Client: "League of Legends"
            // EU Client: "League of Legends EU"
            // etc.
            if (dir.Name.Contains("League of Legends") == true)
            {
                result = OpenDirectory(dir, logger);
            }
            else if (dir.Name.Contains("lol_game_client") == true)
            {
                result = OpenDirectory(dir, logger);
            }
            else
            {
                //
                // Standard Case
                //

                switch (dir.Name)
                {
                    case "rads":
                    case "RADS":
                        {
                            result = OpenDirectory(dir, logger);
                            break;
                        };
                    case "projects":
                        {
                            result = OpenDirectory(dir, logger);
                            break;
                        };
                    case "filearchives":
                        {
                            result = OpenModelsRoot(dir, logger);
                            break;
                        };
                    default:
                        {
                            // Just ignore this directory.
                            break;
                        }
                };

            }

            return result;
        }

        private bool OpenDirectory(DirectoryInfo dir, EventLogger logger)
        {
            bool result = true;

            // Open this directory and keep reading more directories.
            try
            {
                DirectoryInfo di = new DirectoryInfo(dir.FullName);
                foreach (DirectoryInfo d in di.GetDirectories())
                {
                    result = ReadDirectory(d, logger);
                    if (result == false)
                        break;
                }
            }
            catch(Exception e)
            {
                logger.LogError( "Unable to open directory: " + dir.FullName );
                logger.LogError(e.Message);
                result = false;
            }

            return result;
        }

        private bool OpenModelsRoot(DirectoryInfo dir, EventLogger logger)
        {
            bool result = true;

            // We've arrived at the root of the model folders.
            try
            {
                DirectoryInfo di = new DirectoryInfo(dir.FullName);

                // Read directories in reverse order to prioritize newer files.
                DirectoryInfo[] children = di.GetDirectories();
                for (int i = 1; i <= children.Length; ++i)
                {
                    result = OpenGameClientVersion(children[children.Length - i], logger);
                    if (result == false)
                        break;
                }
            }
            catch(Exception e)
            {
                logger.LogError("Unable to open directory: " + dir.FullName);
                logger.LogError(e.Message);
                result = false;
            }

            return result;
        }

        private bool OpenGameClientVersion(DirectoryInfo dir, EventLogger logger)
        {
            bool result = true;

            // Read in .raf files and look for model information in them.
            RAFArchive archive = null;
            try
            {
                foreach(FileInfo f in dir.GetFiles())
                {
                    // Ignore non RAF files.
                    if (f.Extension != ".raf")
                        continue;
                    
                    // ReadRAF() opens the archive.
                    result = ReadRAF(f, ref archive, logger);
                    if (result == false)
                    {
                        break;
                    }
                }
            }
            catch(Exception e)
            {
                logger.LogError("Unable to open directory: " + dir.FullName);
                logger.LogError(e.Message);
                result = false;
            }

            return result;
        }

        private bool ReadRAF(FileInfo f, ref RAFArchive archive, EventLogger logger)
        {
            bool result = true;

            try
            {
                logger.LogEvent("Opening RAF file: " + f.FullName);

                // Open the archive
                archive = new RAFArchive(f.FullName);

                // Get the texture files.
                List<RAFFileListEntry> files = archive.SearchFileEntries(".dds", RAFArchive.RAFSearchType.All);
                foreach (RAFFileListEntry e in files)
                {
                    // Try to parse out unwanted textures.
                    if (e.FileName.Contains("LoadScreen") == false &&
                        e.FileName.Contains("Loadscreen") == false &&
                        e.FileName.Contains("loadscreen") == false &&
                        e.FileName.Contains("circle") == false &&
                        e.FileName.Contains("square") == false &&
                        e.FileName.Contains("DATA") == true &&
                        e.FileName.Contains("Characters") == true)
                    {
                        String name = e.FileName;
                        int pos = name.LastIndexOf("/");
                        name = name.Substring(pos + 1);
                        name = name.ToLower();

                        if (textures.ContainsKey(name) == false)
                        {
                            logger.LogEvent("Adding texture " + name + ": " + e.FileName);
                            textures.Add(name, e);
                        }
                        else
                        {
                            logger.LogWarning("Duplicate texture " + name + ": " + e.FileName);
                        }
                    }
                }

                files = archive.SearchFileEntries(".DDS", RAFArchive.RAFSearchType.All);
                foreach (RAFFileListEntry e in files)
                {
                    // Try to parse out unwanted textures.
                    if (e.FileName.Contains("LoadScreen") == false &&
                        e.FileName.Contains("Loadscreen") == false &&
                        e.FileName.Contains("loadscreen") == false &&
                        e.FileName.Contains("circle") == false &&
                        e.FileName.Contains("square") == false &&
                        e.FileName.Contains("DATA") == true &&
                        e.FileName.Contains("Characters") == true)
                    {
                        String name = e.FileName;
                        int pos = name.LastIndexOf("/");
                        name = name.Substring(pos + 1);
                        name = name.ToLower();

                        if (textures.ContainsKey(name) == false)
                        {
                            logger.LogEvent("Adding texture " + name + ": " + e.FileName);
                            textures.Add(name, e);
                        }
                        else
                        {
                            logger.LogWarning("Duplicate texture " + name + ": " + e.FileName);
                        }
                    }
                }

                // Get the .skn files
                files = archive.SearchFileEntries(".skn", RAFArchive.RAFSearchType.All);
                foreach (RAFFileListEntry e in files)
                {
                    String name = e.FileName;
                    int pos = name.LastIndexOf("/");
                    name = name.Substring(pos + 1);
                    name = name.ToLower();

                    if (skns.ContainsKey(name) == false)
                    {
                        logger.LogEvent("Adding skn " + name + ": " + e.FileName);
                        skns.Add(name, e);
                    }
                    else
                    {
                        logger.LogWarning("Duplicate skn " + name + ": " + e.FileName);
                    }
                }

                // Get the .skl files.
                files = archive.SearchFileEntries(".skl", RAFArchive.RAFSearchType.All);
                foreach (RAFFileListEntry e in files)
                {
                    String name = e.FileName;
                    int pos = name.LastIndexOf("/");
                    name = name.Substring(pos + 1);
                    name = name.ToLower();

                    if (skls.ContainsKey(name) == false)
                    {
                        logger.LogEvent("Adding skl " + name + ": " + e.FileName);
                        skls.Add(name, e);
                    }
                    else
                    {
                        logger.LogWarning("Duplicate skl " + name + ": " + e.FileName);
                    }
                }

                // There's .inibin files in here too.
                files = archive.SearchFileEntries(".inibin", RAFArchive.RAFSearchType.All);
                foreach (RAFFileListEntry e in files)
                {
                    String name = e.FileName;
                    if (name.Contains("Characters") == true) // try to only read required files
                    {
                        logger.LogEvent("Adding inibin " + name + ": " + e.FileName);
                        inibins.Add(e);
                    }
                    else
                    {
                        logger.LogWarning("Excluding inibin " + name + ": " + e.FileName);
                    }
                }

                // Read in animation lists
                files = archive.SearchFileEntries("Animations.list", RAFArchive.RAFSearchType.All);
                foreach (RAFFileListEntry e in files)
                {
                    String name = e.FileName;

                    // Remove the file name.
                    int pos = name.LastIndexOf("/");
                    name = name.Remove(pos);

                    // Remove proceeding directories.
                    pos = name.LastIndexOf("/");
                    name = name.Substring(pos + 1);
                    name = name.ToLower();

                    // Name is the parent directory.
                    if (animationLists.ContainsKey(name) == false)
                    {
                        logger.LogEvent("Adding animation list " + name + ": " + e.FileName);
                        animationLists.Add(name, e);
                    }
                    else
                    {
                        logger.LogWarning("Duplicate animation list " + name + ": " + e.FileName);
                    }
                }

                // Read in animations
                files = archive.SearchFileEntries(".anm", RAFArchive.RAFSearchType.All);
                foreach (RAFFileListEntry e in files)
                {
                    String name = e.FileName;
                    int pos = name.LastIndexOf("/");
                    name = name.Substring(pos + 1);
                    name = name.Remove(name.Length - 4);
                    name = name.ToLower();

                    if (animations.ContainsKey(name) == false)
                    {
                        logger.LogEvent("Adding anm " + name + ": " + e.FileName);
                        animations.Add(name, e);
                    }
                    else
                    {
                        logger.LogWarning("Duplicate anm " + name + ": " + e.FileName);
                    }
                }
            }
            catch(Exception e)
            {
                logger.LogError("Unable to open RAF: " + f.FullName);
                logger.LogError(e.Message);
                result = false;
            }

            return result;
        }

        //
        // Helper Functions
        //
        private bool ContainsDirectory(DirectoryInfo parent, String child)
        {
            bool result = false;

            // Sanity.
            if (parent.Exists == true)
            {
                try
                {
                    foreach (DirectoryInfo d in parent.GetDirectories())
                    {
                        if (d.Name.Contains(child) == true) // contains used for the LOL NA - LOL EU case
                        {
                            result = true;
                            break;
                        }
                    }
                }
                catch { }
            }

            return result;
        }
    }
}


