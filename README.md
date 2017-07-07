# PHP MacroEngine for Umbraco

[![No Maintenance Intended](http://unmaintained.tech/badge.svg)](http://unmaintained.tech/)

This package allows you to write Umbraco macros in the PHP language.

Do you have designers or web devs that only know PHP? Now they can particiapte on Umbraco projects too!

***This package is very much an experimental prototype. I would not recommend that is used in a production environment.***

The underlying code relies on the [Phalanger PHP compiler](http://phalanger.codeplex.com/) (for .NET and Mono). The Phalanger assemblies are included in the package.

### Example usage

To reference an external PHP script:

	<umbraco:Macro runat="server" Language="php" FileLocation="~/macroScripts/test.php" />

To use PHP inside the Macro control in-line:

	<umbraco:Macro runat="server" Language="php">
		<h1><?php echo $model->Name; ?></h1>
		<ul>
		<?php
			foreach ($model->Properties as $property)
			{
				echo "<li>$property->Alias: $property->Value</li>";
			}
		?>
		</ul>
	</umbraco:Macro>


### References

* [Phalanger](http://phalanger.codeplex.com/)
	* [PHP as a scripting language for C#](http://www.php-compiler.net/blog/2011/php-code-c-sharp)
	* [Installation-Free Phalanger web](http://www.php-compiler.net/blog/2011/installation-free-phalanger-web)
* Code snippet examples taken from [PHP View Engine](http://phpviewengine.codeplex.com/).

### Contact

* Twitter: [@leekelleher](http://twitter.com/leekelleher)
