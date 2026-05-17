================================================================================
LICENSING — HOW TO USE THE FILES IN THIS PACKAGE
===

1. PLACE THE FILES

   Drop these two files into the root of your inBEP repository, alongside
your .csproj:

   LICENSE     ← the full Apache 2.0 text
NOTICE      ← attribution to you and the BEP algorithm

   That's the bare minimum. Apache 2.0 doesn't require anything else at the
repo level.

2. ADD A SHORT FILE-LEVEL HEADER (optional but recommended)

   Your current source files already have a footer line like:

   // Author: Rich Wagner — newdawndata.com — Apache 2.0


   This is fine, but the Apache "Appendix" boilerplate is the form that
automated license scanners (FOSSA, ScanCode, GitHub's license detector)
recognize unambiguously. If you want maximum machine-readability, replace
the footer with the short header below. If you don't care about scanners
and prefer your current style, leave it alone — both satisfy the license.

   Short header (paste at the very top of each .cs file):

   // Copyright 2025 Rich Wagner
       //
       // Licensed under the Apache License, Version 2.0 (the "License");
       // you may not use this file except in compliance with the License.
       // You may obtain a copy of the License at
       //
       //     http://www.apache.org/licenses/LICENSE-2.0
       //
       // Unless required by applicable law or agreed to in writing, software
       // distributed under the License is distributed on an "AS IS" BASIS,
       // WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
       // implied. See the License for the specific language governing
       // permissions and limitations under the License.


   The SPDX one-liner variant — which most scanners also recognize — is even
shorter and pairs well with your existing comment style:

   // SPDX-License-Identifier: Apache-2.0
       // Copyright 2025 Rich Wagner — newdawndata.com


3. UPDATE README.md (recommended)

   Add a "License" section at the bottom:

   \## License

       Licensed under the Apache License, Version 2.0. See \[LICENSE](LICENSE)
       for the full text and \[NOTICE](NOTICE) for attribution requirements.

       The Binary Equation Path (BEP) algorithm and the inBEP codec
       family are the original work of Rich Wagner. When redistributing
       this software or publishing benchmarks/results that incorporate it,
       please preserve the attribution as required by the NOTICE file.


4. UPDATE THE .csproj (optional, helps NuGet consumers)

   If you ever publish inBEP as a NuGet package, add these properties
to the <PropertyGroup> in inBEP.csproj:

   <PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>
       <Authors>Rich Wagner</Authors>
       <Copyright>Copyright 2025 Rich Wagner</Copyright>
       <PackageProjectUrl>https://newdawndata.com</PackageProjectUrl>


   These show up on the NuGet listing page and on nuget.org's search results.

   ================================================================================
WHAT THIS GIVES YOU
   ===

   • Anyone can use, modify, and ship BEP — commercial or otherwise.
• They MUST preserve the NOTICE file contents when redistributing.
• They MUST state if they modified your files.
• They get an explicit patent grant from you (and you from them, if they
contribute back). No surprise patent ambushes either direction.
• You retain full ownership of the algorithm and the code.
• You can dual-license later (e.g., add a commercial license) if your
plans change. Apache 2.0 doesn't lock you out of that.

   ================================================================================
WHAT THIS DOES NOT GIVE YOU
   ===

   • Trademark protection. If you want "inBEP" or "BEP" to be a
protected mark, that's a separate filing with the USPTO (or equivalent
in your country). Most open-source projects don't bother unless they
grow large enough to attract trademark squatters.
• A contributor license agreement (CLA). If you start accepting pull
requests from outside contributors and want belt-and-suspenders clarity
that they assign rights to you, look at the Apache Individual CLA
template or the Developer Certificate of Origin (DCO) lightweight
alternative. Neither is needed for a solo project.
• Protection against people running benchmarks against BEP and publishing
results without crediting you. That's an academic/community norm
question, not a license question. The NOTICE language is the strongest
nudge you can put in the license itself.

   ================================================================================
WHEN TO REVISIT THIS
   ===

   • If you start a company around BEP and want a commercial license tier,
look at dual-licensing (Apache OR Commercial).
• If outside contributors start sending substantial patches, consider
adopting the DCO ("Signed-off-by:" line on commits) — it's the GPL/Linux
model and adds almost no friction.
• If you submit BEP to a standards body or want it inside a foundation
(Apache Software Foundation, OpenJS, CNCF, etc.), they have their own
contribution and IP processes you'll need to follow.

