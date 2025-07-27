# Requirements Document

## Introduction

This feature specification outlines the development of a Windows Explorer-style file explorer for Path of Exile's GGPK (Game Game Pack) files using C# WPF with WPF-UI library. The application will provide an intuitive, familiar interface for browsing and exploring the hierarchical structure of GGPK files, including support for both traditional GGPK files and modern bundle-based assets. The explorer will decompress and display the _.index.bin file contents using TreeNode structures, closely resembling the Windows File Explorer experience with modern Fluent Design.

## Requirements

### Requirement 1

**User Story:** As a Path of Exile developer or modder, I want to open and browse GGPK files through a familiar Windows Explorer-style interface, so that I can easily navigate the game's asset structure.

#### Acceptance Criteria

1. WHEN the user launches the application THEN the system SHALL display a FluentWindow with a Windows Explorer-style layout using WPF-UI NavigationView
2. WHEN the user clicks "Open GGPK File" or uses File menu THEN the system SHALL present a file dialog to select Content.ggpk files
3. WHEN a valid GGPK file is selected THEN the system SHALL load and display the file structure in a WPF TreeView control
4. IF the GGPK file is corrupted or invalid THEN the system SHALL display an appropriate error message using WPF-UI InfoBar
5. WHEN the GGPK file is successfully loaded THEN the system SHALL show the root directory structure in the left panel

### Requirement 2

**User Story:** As a user exploring GGPK files, I want to see a dual-pane layout similar to Windows Explorer, so that I can navigate directories in one pane and view file details in another.

#### Acceptance Criteria

1. WHEN the application loads THEN the system SHALL display a split-pane interface using WPF Grid with TreeView on the left and details view on the right
2. WHEN the user clicks on a directory in the TreeView THEN the system SHALL populate the right pane with the directory's contents
3. WHEN the user expands a directory node THEN the system SHALL load child directories and files lazily for performance
4. WHEN displaying directory contents THEN the system SHALL show file names, types, sizes, and modification dates in a WPF ListView with GridView
5. WHEN the user double-clicks a directory in the right pane THEN the system SHALL navigate to that directory and update the TreeView selection

### Requirement 3

**User Story:** As a user working with bundled GGPK files, I want the application to automatically detect and decompress _.index.bin files, so that I can access the complete file structure including bundled content.

#### Acceptance Criteria

1. WHEN opening a GGPK file THEN the system SHALL detect if it contains bundled content using BundledGGPK class
2. WHEN _.index.bin is found THEN the system SHALL decompress it using the appropriate TreeNode structures
3. WHEN displaying bundled content THEN the system SHALL integrate bundle files seamlessly with regular GGPK files in the TreeView
4. WHEN accessing bundle files THEN the system SHALL use the oo2core.dll for Oodle compression/decompression
5. IF bundle decompression fails THEN the system SHALL log the error and continue with available content

### Requirement 4

**User Story:** As a user browsing GGPK files, I want to see appropriate icons and visual indicators for different file types and directories, so that I can quickly identify content types.

#### Acceptance Criteria

1. WHEN displaying directories THEN the system SHALL show folder icons using WPF-UI SymbolIcon with Fluent Design icons
2. WHEN displaying files THEN the system SHALL show appropriate file type icons based on file extensions using FontIcon
3. WHEN showing bundle files THEN the system SHALL use distinct icons to indicate bundled content
4. WHEN a directory is being loaded THEN the system SHALL show a loading indicator using WPF-UI ProgressRing
5. WHEN files are compressed THEN the system SHALL display compression status indicators using WPF-UI InfoBadge

### Requirement 5

**User Story:** As a user exploring large GGPK files, I want the application to load content efficiently and responsively, so that navigation remains smooth even with large file structures.

#### Acceptance Criteria

1. WHEN loading GGPK files THEN the system SHALL implement lazy loading for directory contents using single-threaded operations
2. WHEN expanding TreeView nodes THEN the system SHALL load children on-demand to minimize memory usage in a single thread
3. WHEN displaying large directories THEN the system SHALL implement virtualization in the ListView
4. WHEN performing file operations THEN the system SHALL show progress indicators for long-running tasks while maintaining single-threaded GGPK access
5. WHEN the user cancels a loading operation THEN the system SHALL gracefully stop the operation without thread conflicts

### Requirement 6

**User Story:** As a user working with GGPK files, I want to extract individual files or entire directories to my local file system, so that I can work with the assets outside the GGPK container.

#### Acceptance Criteria

1. WHEN the user right-clicks on a file THEN the system SHALL show a context menu with "Extract" option
2. WHEN the user selects "Extract" THEN the system SHALL open a folder browser dialog to choose destination
3. WHEN extracting files THEN the system SHALL preserve the original file structure and metadata
4. WHEN extracting directories THEN the system SHALL recursively extract all contained files and subdirectories
5. WHEN extraction is complete THEN the system SHALL display a success message with the extraction location

### Requirement 7

**User Story:** As a user navigating GGPK files, I want search functionality to quickly locate specific files or directories, so that I can find assets efficiently in large file structures.

#### Acceptance Criteria

1. WHEN the user enters text in the search box THEN the system SHALL filter the current directory view in real-time
2. WHEN performing a global search THEN the system SHALL search across the entire GGPK file structure
3. WHEN search results are displayed THEN the system SHALL highlight matching text and show file paths
4. WHEN the user clicks on a search result THEN the system SHALL navigate to the file's location in the TreeView
5. WHEN clearing the search THEN the system SHALL restore the normal directory view

### Requirement 8

**User Story:** As a user working with different GGPK file versions, I want the application to handle various GGPK formats and provide appropriate error handling, so that I can work with files from different Path of Exile versions.

#### Acceptance Criteria

1. WHEN opening GGPK files THEN the system SHALL detect and support different GGPK versions automatically using single-threaded access
2. WHEN encountering unsupported formats THEN the system SHALL display clear error messages with version information
3. WHEN file corruption is detected THEN the system SHALL attempt recovery and report recoverable vs. fatal errors
4. WHEN bundle format changes THEN the system SHALL gracefully handle format differences using LibBundle3 in single-threaded mode
5. WHEN errors occur THEN the system SHALL log detailed error information for debugging purposes and maintain thread safety

### Requirement 9

**User Story:** As a user exploring GGPK files, I want to view file properties and metadata, so that I can understand file characteristics and relationships.

#### Acceptance Criteria

1. WHEN the user right-clicks on a file THEN the system SHALL show "Properties" option in the context menu
2. WHEN "Properties" is selected THEN the system SHALL display a dialog with file metadata including size, hash, and offset
3. WHEN viewing directory properties THEN the system SHALL show total size, file count, and subdirectory count
4. WHEN displaying bundle file properties THEN the system SHALL show compression information and bundle details
5. WHEN properties dialog is open THEN the system SHALL allow copying property values to clipboard

### Requirement 10

**User Story:** As a user working with GGPK files regularly, I want the application to remember my preferences and recently opened files, so that I can work more efficiently.

#### Acceptance Criteria

1. WHEN the application starts THEN the system SHALL restore the last window size and position
2. WHEN GGPK files are opened THEN the system SHALL add them to a recent files list
3. WHEN the user accesses File menu THEN the system SHALL display recently opened GGPK files
4. WHEN the user changes view settings THEN the system SHALL persist these preferences
5. WHEN the application closes THEN the system SHALL save all user preferences and settings

### Requirement 11

**User Story:** As a user working with GGPK files, I want all file operations to be performed in a single thread to ensure data integrity and prevent corruption, so that I can safely work with Path of Exile assets.

#### Acceptance Criteria

1. WHEN performing any GGPK file operation THEN the system SHALL execute all LibGGPK3 operations on a single dedicated thread
2. WHEN multiple operations are requested THEN the system SHALL queue them for sequential execution on the GGPK thread
3. WHEN UI updates are needed THEN the system SHALL marshal results back to the UI thread safely
4. WHEN long-running operations occur THEN the system SHALL provide cancellation support while maintaining single-threaded access
5. WHEN disposing GGPK resources THEN the system SHALL ensure proper cleanup on the same thread that opened the file