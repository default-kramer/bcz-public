#lang racket/gui
(require pict)

; We start with a rectangle.
; Then we randomly add some half crossers on the left side
; and some half crossers on the right side.
; For each half crosser that is not connected, we randomly choose
; up or down and make a run until we hit another (either left or right).
; This leaves us with new rectangles (but possibly incomplete)
; So let's see... How about we model a rectangle as
; * Height - a natural number, also the ratio of PixelHeight / PixelWidth
; * Data - an array of 2*height, where each cell knows
;   ** left? - do we connect to the left side here?
;   ** right? - do we connect to the right side here?
;   ** vert? - do we have a vertical bar?
; So we have
; X__ X
; X   X left only
; X   X
; X __X
; X   X right only
; X   X
; X___X
; X   X left and right
; X   X
; X__ X
; X | X left and vert
; X   X
; X___X
; X | X all 3
; X   X

(define (rset! rect y item)
  (vector-set! rect y (cons item (vector-ref rect y))))

(define (rect-slots rect)
  (vector-length rect))

(define (rect-has? rect y . items)
  (and (ormap (lambda (item)
                (member item (vector-ref rect y)))
              items)
       #t))

(define (in-bounds? rect y)
  (and (>= y 0)
       (< y (rect-slots rect))))

(define (add-crossers! rect)
  (for ([y (in-range 1 (rect-slots rect))])
    (when (and (= 0 (random 3))
               (rect-has? rect y 'lside))
      (rset! rect y 'left))
    (when (and (= 0 (random 3))
               (rect-has? rect y 'rside))
      (rset! rect y 'right))))

(define (add-verticals! rect)
  (for ([y (in-range (rect-slots rect))])
    (when (rect-has? rect y 'left 'right)
      (let ([down? (= 0 (random 2))])
        (define (go y)
          (cond
            [(not (in-bounds? rect y)) #f]
            [down?
             (let ([next (add1 y)])
               (rset! rect y 'vert)
               (when (and (in-bounds? rect next)
                          (not (rect-has? rect next 'right 'left 'vert)))
                 (go next)))]
            [else
             (begin
               (rset! rect y 'vert)
               (when  (not (rect-has? rect y 'right 'left))
                 (go (sub1 y))))]))
        (go (if down? y (sub1 y)))))))

(define line-color
  (make-parameter (make-object color% 74 85 104)))

(define (draw rect size [thickness 1])
  (let ([line-color (line-color)])
    (dc (lambda (dc dx dy)
          (define old-pen (send dc get-pen))
          (send dc set-pen (new pen%
                                [width thickness]
                                ;[style 'hilite]
                                [color line-color]))
          (for ([y (in-range (rect-slots rect))])
            (let* ([top (+ dy (* y size))]
                   [bottom (+ top size)]
                   [left dx]
                   [middle (+ left size)]
                   [right (+ middle size)])
              (when (rect-has? rect y 'lside)
                (send dc draw-line left top left bottom))
              (when (rect-has? rect y 'left)
                (send dc draw-line left top middle top))
              (when (rect-has? rect y 'right)
                (send dc draw-line middle top right top))
              (when (rect-has? rect y 'vert)
                (send dc draw-line middle top middle bottom))))
          (send dc set-pen old-pen))
        (* size 2)
        (* size (rect-slots rect)))))

(define V (make-vector 20 '(lside rside)))
(add-crossers! V)
(draw V 20)
(println "---")
(add-verticals! V)
(draw V 20)

(define (split rect)
  (define L (make-vector (* 2 (rect-slots rect)) '(lside)))
  (define R (make-vector (* 2 (rect-slots rect)) '(rside)))
  (define (set-side! A val)
    (for ([y (in-range (rect-slots rect))])
      (when (rect-has? rect y 'vert)
        (rset! A (+ 0 (* 2 y)) val)
        (rset! A (+ 1 (* 2 y)) val))))
  (set-side! L 'rside)
  (set-side! R 'lside)
  (values L R))

(define-values (L R) (split V))
(add-crossers! L)
(add-verticals! L)
(add-crossers! R)
(add-verticals! R)

; Forgot how this code works... but h roughly controls the height
(define (make-column #:height h)
  (let*-values ([(V) (make-vector h '(lside rside))]
                [(_) (add-crossers! V)]
                [(_) (add-verticals! V)]
                [(L R) (split V)])
    (add-crossers! L)
    (add-crossers! R)
    (add-verticals! L)
    (add-verticals! R)
    (cc-superimpose (draw V 16 1)
                    (hc-append (draw L 8 1)
                               (draw R 8 1)))))

(define the-pict
  (apply hc-append
         (for/list ([i (in-range 30)])
           (make-column #:height 45))))

(define pict-2
  (scale
   (parameterize ([line-color (make-object color% 45 55 72)]); 26 32 44)])
     (apply hc-append
            (for/list ([i (in-range 10)])
              (make-column #:height 15))))
   3))

(list "same size?"
      (pict-width the-pict) (pict-width pict-2)
      (pict-height the-pict) (pict-height pict-2))
;the-pict

;(send (pict->bitmap the-pict) save-file "background.png" 'png)
(define both (lt-superimpose the-pict pict-2))

(define bg
  (filled-rectangle (pict-width both)
                    (pict-height both)
                    #:draw-border? #f
                    #:color (make-object color% 26 32 44))); 45 55 72)))

(define with-bg
  (lt-superimpose bg both))

with-bg
(send (pict->bitmap with-bg) save-file "background.png" 'png)
