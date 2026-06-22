// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "SpacePilotMac",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .executable(name: "SpacePilotMac", targets: ["SpacePilotMac"])
    ],
    targets: [
        .executableTarget(
            name: "SpacePilotMac",
            path: "Sources/SpacePilotMac"
        ),
        .testTarget(
            name: "SpacePilotMacTests",
            dependencies: ["SpacePilotMac"],
            path: "Tests/SpacePilotMacTests"
        )
    ]
)
