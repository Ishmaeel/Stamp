![Icon](https://raw.github.com/Fody/Stamp/master/Icons/package_icon.png)

### This is an add-in for [Fody](https://github.com/Fody/Fody/) 

Stamps an assembly with svn data.

### Nuget package

Available here http://nuget.org/packages/StampSvn.Fody 

To Install from the Nuget Package Manager Console 
    
    PM> Install-Package StampSvn.Fody

## What it does 

AssemblyVersion and AssemblyFileVersion is rewritten so that they contain svn revision number as their 'revision' fields.

1.0.0.0 becomes 1.0.{svnRev}.0

Extracts the svn information from disk, combines it with the assembly version, and places it in the `AssemblyInformationalVersionAttribute`.

So if your assembly version is 1.0.0.0, the working path is "/repo/branches/F101" and the last commit is 850 and you have pending changes then the following attribute will be added to the assembly.

	[assembly: AssemblyInformationalVersion("1.0.850.0 Path:'/repo/branches/F101' Rev:850 HasPendingChanges")]
	
## Templating the version

You can customize the string used in the `AssemblyInformationalVersionAttribute` by adding some tokens to the string, which StampSvn will replace.

For example, if you add `[assembly: AssemblyInformationalVersion("%version% Branch=%branch%")]` then StampSvn will change it to `[assembly: AssemblyInformationalVersion("1.0.850.0 Branch=/repo/branches/F101")]`

The tokens are:
- `%version%` is replaced with the version (1.0.0.0)
- `%version1%` is replaced with the major version only (1)
- `%version2%` is replaced with the major and minor version (1.0)
- `%version3%` is replaced with the major, minor, and revision version (1.0.0)
- `%version4%` is replaced with the major, minor, revision, and build version (1.0.0.0)
- `%revno%` is replaced with the largest svn revision number in the repository
- `%branch%` is replaced with the branch name of the repository
- `%haschanges%` is replaced with the string "HasChanges" if the repository is dirty, else a blank string
- `%user%` is replaced with the username of MSBuild process
- `%machine%` is replaced with Environment.MachineName

## Icon

<a href="http://thenounproject.com/noun/stamp/#icon-No8787" target="_blank">Stamp</a> designed by <a href="http://thenounproject.com/rohithdezinr" target="_blank">Rohith M S</a> from The Noun Project