﻿using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Kiss.Utils
{
    public sealed class FileUtil
    {
        // The "_mkdir" function is used by the "CreateDirectory" method.
        [DllImport ( "msvcrt.dll", SetLastError = true )]
        private static extern int _mkdir ( string path );

        private FileUtil ( )
        { }

        /// <summary>
        /// This method should provide safe substitude for Directory.CreateDirectory().
        /// </summary>
        /// <param name="path">The directory path to be created.</param>
        /// <returns>A <see cref="System.IO.DirectoryInfo"/> object for the created directory.</returns>
        /// <remarks>
        ///		<para>
        ///		This method creates all the directory structure if needed.
        ///		</para>
        ///		<para>
        ///		The System.IO.Directory.CreateDirectory() method has a bug that gives an
        ///		error when trying to create a directory and the user has no rights defined
        ///		in one of its parent directories. The CreateDirectory() should be a good 
        ///		replacement to solve this problem.
        ///		</para>
        /// </remarks>
        public static DirectoryInfo CreateDirectory ( string path )
        {
            // Create the directory info object for that dir (normalized to its absolute representation).
            DirectoryInfo oDir = new DirectoryInfo ( Path.GetFullPath ( path ) );

            try
            {
                // Try to create the directory by using standard .Net features. (#415)
                if ( !oDir.Exists )
                    oDir.Create ( );

                return oDir;
            }
            catch ( Exception ex )
            {
                TxtLogger.DumpException ( ex );
                CreateDirectoryUsingDll ( oDir );

                return new DirectoryInfo ( path );
            }
        }

        private static void CreateDirectoryUsingDll ( DirectoryInfo dir )
        {
            // On some occasion, the DirectoryInfo.Create() function will 
            // throw an error due to a bug in the .Net Framework design. For
            // example, it may happen that the user has no permissions to
            // list entries in a lower level in the directory path, and the
            // Create() call will simply fail.
            // To workaround it, we use mkdir directly.

            ArrayList oDirsToCreate = new ArrayList ( );

            // Check the entire path structure to find directories that must be created.
            while ( dir != null && !dir.Exists )
            {
                oDirsToCreate.Add ( dir.FullName );
                dir = dir.Parent;
            }

            // "dir == null" means that the check arrives in the root and it doesn't exist too.
            if ( dir == null )
                throw ( new System.IO.DirectoryNotFoundException ( "Directory \"" + oDirsToCreate[ oDirsToCreate.Count - 1 ] + "\" not found." ) );

            // Create all directories that must be created (from bottom to top).
            for ( int i = oDirsToCreate.Count - 1 ; i >= 0 ; i-- )
            {
                string sPath = ( string ) oDirsToCreate[ i ];
                int iReturn = _mkdir ( sPath );

                if ( iReturn != 0 )
                    throw new ApplicationException ( "Error calling [msvcrt.dll]:_wmkdir(" + sPath + "), error code: " + iReturn );
            }
        }

        public static bool ArrayContains ( Array array, object value, System.Collections.IComparer comparer )
        {
            foreach ( object item in array )
            {
                if ( comparer.Compare ( item, value ) == 0 )
                    return true;
            }
            return false;
        }

        public static void CopyDirectory ( string src, string dst, List<string> extensions )
        {
            if ( dst[ dst.Length - 1 ] != Path.DirectorySeparatorChar )
                dst += Path.DirectorySeparatorChar;

            if ( !Directory.Exists ( dst ) )
                Directory.CreateDirectory ( dst );

            foreach ( string Element in Directory.GetFileSystemEntries ( src ) )
            {
                if ( extensions != null && !extensions.Contains ( Path.GetExtension ( Element ) ) )
                    continue;

                if ( Directory.Exists ( Element ) )
                    CopyDirectory ( Element, dst + Path.GetFileName ( Element ), extensions );
                else
                    File.Copy ( Element, dst + Path.GetFileName ( Element ), true );
            }
        }
    }
}
