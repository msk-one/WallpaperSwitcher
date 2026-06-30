import AppKit

guard CommandLine.arguments.count >= 2 else {
    fatalError("Usage: swift render-wallpaper-switcher-icon.swift <app-icon-output> [tray-icon-output]")
}

let appIconOutputPath = CommandLine.arguments[1]
let trayIconOutputPath = CommandLine.arguments.count > 2 ? CommandLine.arguments[2] : nil
let size = 1024
let scale = CGFloat(size)

func color(_ hex: UInt32) -> NSColor {
    let red = CGFloat((hex >> 16) & 0xff) / 255.0
    let green = CGFloat((hex >> 8) & 0xff) / 255.0
    let blue = CGFloat(hex & 0xff) / 255.0
    return NSColor(calibratedRed: red, green: green, blue: blue, alpha: 1.0)
}

func rect(_ x: CGFloat, _ y: CGFloat, _ width: CGFloat, _ height: CGFloat) -> NSRect {
    NSRect(x: x * scale, y: y * scale, width: width * scale, height: height * scale)
}

func writePng(_ image: NSImage, to outputPath: String) throws {
    guard
        let tiff = image.tiffRepresentation,
        let bitmap = NSBitmapImageRep(data: tiff),
        let png = bitmap.representation(using: .png, properties: [:])
    else {
        fatalError("Unable to render PNG")
    }

    try png.write(to: URL(fileURLWithPath: outputPath), options: .atomic)
}

func drawBlissBackground() {
    let bounds = NSRect(x: 0, y: 0, width: size, height: size)
    let outer = NSBezierPath(roundedRect: bounds, xRadius: 210, yRadius: 210)
    outer.addClip()

    NSGradient(colors: [color(0x7EC8FF), color(0x2F95E5), color(0x0054E3)])?
        .draw(in: bounds, angle: -52)

    NSGradient(colors: [color(0xC9E66D), color(0x62B735), color(0x1D7B2D)])?
        .draw(in: rect(0.0, 0.0, 1.0, 0.42), angle: -12)

    color(0x7FD143).withAlphaComponent(0.76).setFill()
    let hill = NSBezierPath()
    hill.move(to: NSPoint(x: 0, y: 0.16 * scale))
    hill.curve(
        to: NSPoint(x: 0.52 * scale, y: 0.28 * scale),
        controlPoint1: NSPoint(x: 0.15 * scale, y: 0.30 * scale),
        controlPoint2: NSPoint(x: 0.34 * scale, y: 0.34 * scale))
    hill.curve(
        to: NSPoint(x: scale, y: 0.34 * scale),
        controlPoint1: NSPoint(x: 0.74 * scale, y: 0.20 * scale),
        controlPoint2: NSPoint(x: 0.83 * scale, y: 0.38 * scale))
    hill.line(to: NSPoint(x: scale, y: 0))
    hill.line(to: NSPoint(x: 0, y: 0))
    hill.close()
    hill.fill()
}

func drawCenteredText(font: NSFont, y: CGFloat, height: CGFloat, size: CGFloat, kern: CGFloat, shadow: Bool) {
    let paragraph = NSMutableParagraphStyle()
    paragraph.alignment = .center

    if shadow {
        let textShadow = NSShadow()
        textShadow.shadowOffset = NSSize(width: 0, height: -16)
        textShadow.shadowBlurRadius = 18
        textShadow.shadowColor = color(0x1D4D8F).withAlphaComponent(0.38)
        textShadow.set()
    }

    let attrs: [NSAttributedString.Key: Any] = [
        .font: font,
        .foregroundColor: NSColor.white,
        .paragraphStyle: paragraph,
        .kern: kern
    ]
    ("WS" as NSString).draw(in: rect(0.0, y, 1.0, height), withAttributes: attrs)
    NSShadow().set()
}

let appIcon = NSImage(size: NSSize(width: size, height: size))
appIcon.lockFocus()
NSGraphicsContext.current?.imageInterpolation = .high
drawBlissBackground()
let appFont = NSFont(name: "Avenir Next Heavy", size: 430)
    ?? NSFont.systemFont(ofSize: 430, weight: .heavy)
drawCenteredText(font: appFont, y: 0.32, height: 0.42, size: 430, kern: -20, shadow: true)
appIcon.unlockFocus()
try writePng(appIcon, to: appIconOutputPath)

if let trayIconOutputPath {
    let trayIcon = NSImage(size: NSSize(width: size, height: size))
    trayIcon.lockFocus()
    NSGraphicsContext.current?.imageInterpolation = .high
    NSColor.clear.setFill()
    NSRect(x: 0, y: 0, width: size, height: size).fill()
    let trayFont = NSFont.systemFont(ofSize: 500, weight: .heavy)
    drawCenteredText(font: trayFont, y: 0.29, height: 0.50, size: 500, kern: -18, shadow: false)
    trayIcon.unlockFocus()
    try writePng(trayIcon, to: trayIconOutputPath)
}
