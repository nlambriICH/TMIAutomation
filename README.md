# TMIAutomation

Plug-in script for the Eclipse Treatment Planning System to automate Total Marrow (Lymph-node) Irradiation (TMI/TMLI).

The script was introduced and validated in [this paper](https://doi.org/10.1007/s00066-022-02014-0).

If you liked/used this project, don't forget to give a star! :star:

## Key features

* Automatic planning of the lower-extremities for TMI/TMLI
* Extendible to VMAT-TBI (Total Body Irradiation delivered with Volumetric Modulated Arc Therapy)

## Usage

### Demo

![demo-ESAPI15](demo/demo_short_ESAPI15.gif)

### Requirements

Tested on **Eclipse v15** and **v16**.

Go to the latest release [page](https://github.com/nlambriICH/TMIAutomation/releases/latest):

* for Eclipse v15: download the zip file `ESAPI15_TMIAutomation-vx.x.x.x.zip`
* for Eclipse v16: download the zip file `ESAPI16_TMIAutomation-vx.x.x.x.zip`

The plug-in script `TMIAutomation.esapi.dll` needs to be **approved** in the Eclipse application.

The `Configuration` folder contains config files with objectives and parameters to optimize the lower-extremities plan.
Example values are provided in order for the script to execute properly.

## Contributing

Any contribution/feedback is **greatly appreciated**!

If you have a suggestion that can improve this project, you can open a new issue [here](https://github.com/nlambriICH/TMIAutomation/issues).

If you want to contribute directly to the code, please follow these steps:

1. Clone this repo from your GitHub account to your local disk: `git clone https://github.com/nlambriICH/TMIAutomation.git`
2. Create your feature branch: `git checkout -b feature-branch`
3. Commit your changes: `git commit -m 'Add new feature'`
4. Push your changes: `git push -u origin feature-branch`
5. Open a pull request [here](https://github.com/nlambriICH/TMIAutomation/pulls)

### Development

Open `TMIAutomation.sln` with [Visual Studio](https://visualstudio.microsoft.com/).

Project structure:

* `TMIAutomation`: plug-in source code
* `TMIAutomation.Runner`: used to run and debug the plug-in
* `TMIAutomation.Tests`: unit tests for the plug-in

## Licence

Distributed under the MIT License. See `LICENSE.txt` for more information.

## Contact

Nicola Lambri - nicola.lambri@cancercenter.humanitas.it
