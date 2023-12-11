# tflite build for android

tested
 tensorflow/tensorflow : 423669ae39d
 asus4/tf-lite-unity-sample : 7eb841e

```
mkdir work
cd work

wget https://raw.githubusercontent.com/tensorflow/tensorflow/master/tensorflow/lite/tools/tflite-android.Dockerfile
docker build . -t tflite-builder -f tflite-android.Dockerfile

git clone https://github.com/tensorflow/tensorflow.git
git clone https://github.com/asus4/tf-lite-unity-sample.git

docker run -it -v $PWD:/host_dir tflite-builder bash

####### docker container begin ########
# https://www.tensorflow.org/lite/android/lite_build
sdkmanager   "build-tools;${ANDROID_BUILD_TOOLS_VERSION}"   "platform-tools"   "platforms;android-${ANDROID_API_LEVEL}"
# accept y

cd /host_dir/tensorflow/

# https://github.com/tensorflow/tensorflow/blob/423669ae39d1ef8f578fd7ae8f3011ea2d9bca69/tensorflow/lite/tools/build_aar_with_docker.sh#L110C1-L119C4
printf '%s\n' '/usr/bin/python3' '/usr/lib/python3/dist-packages' 'N' 'N' 'N' '-Wno-sign-compare -Wno-c++20-designator -Wno-gnu-inline-cpp-without-extern' 'y' | ./configure


cd /host_dir/tf-lite-unity-sample/
sed -i.bak '107,112s/-c opt/-c opt --config=opt/' build_tflite.py 
./build_tflite.py --tfpath ../tensorflow/ -android
####### docker container end  ########

```
