-- Fix existing audiobooks to have Monitored = true by default
-- Run this in the SQLite database if you have audiobooks that aren't showing in the Wanted view

-- Update all audiobooks without files to be monitored
UPDATE Audiobooks 
SET Monitored = 1 
WHERE (FilePath IS NULL OR FilePath = '' OR FilePath = 'null')
AND Monitored = 0;

-- Check the results
SELECT 
    Id,
    Title,
    Monitored,
    FilePath,
    CASE 
        WHEN FilePath IS NULL OR FilePath = '' THEN 'Missing'
        ELSE 'Has File'
    END as FileStatus
FROM Audiobooks
ORDER BY Monitored DESC, Title;
